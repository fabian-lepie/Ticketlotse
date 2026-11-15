using System.Globalization;
using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NUnit.Framework;
using Ticketlotse.Application;
using Ticketlotse.Domain;
using Ticketlotse.Infrastructure;

namespace Ticketlotse.Tests;

public class ClaudeTicketKlassifiziererTests
{
    private const string KiModell = "claude-haiku-4-5";

    private static Ticket NeuesTicket(string text = "Mein Abschlag ist zu hoch.") =>
        Ticket.Erstellen("kunde@example.de", "Abschlag", text);

    private static ClaudeTicketKlassifizierer Klassifizierer(
        FakeHttpHandler handler, string? aufwand = null) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://api.anthropic.com/") },
            Options.Create(new ClaudeEinstellungen { Modell = KiModell, MaxTokens = 2048, Aufwand = aufwand }),
            NullLogger<ClaudeTicketKlassifizierer>.Instance);

    /// <summary>Das JSON, das die KI laut Schema im Textblock liefert.</summary>
    private static string Einordnung(
        string kategorie = "Abschlag",
        string dringlichkeit = "Mittel",
        double konfidenz = 0.82) =>
        $$"""
        {"kategorie":"{{kategorie}}","dringlichkeit":"{{dringlichkeit}}","konfidenz":{{konfidenz.ToString(CultureInfo.InvariantCulture)}},"begruendung":"Begründung der KI","antwortvorschlag":"Antwortvorschlag der KI"}
        """;

    /// <summary>Die Hülle der Messages-API um eine Einordnung.</summary>
    private static string Antwort(
        string nutzlast,
        string modell = KiModell,
        string stopGrund = "end_turn",
        bool mitDenkblock = false)
    {
        var denkblock = mitDenkblock ? """{ "type": "thinking", "thinking": "" },""" : "";

        return $$"""
        {
          "id": "msg_test",
          "type": "message",
          "role": "assistant",
          "model": "{{modell}}",
          "content": [ {{denkblock}} { "type": "text", "text": {{JsonSerializer.Serialize(nutzlast)}} } ],
          "stop_reason": "{{stopGrund}}",
          "usage": { "input_tokens": 10, "output_tokens": 20 }
        }
        """;
    }

    [Test]
    public async Task Eine_gueltige_Antwort_wird_zum_Vorschlag()
    {
        var klassifizierer = Klassifizierer(
            FakeHttpHandler.Antwortet(Antwort(Einordnung(konfidenz: 0.82))));

        var vorschlag = await klassifizierer.EinordnenAsync(NeuesTicket());

        Assert.That(vorschlag.Klassifizierung.Kategorie, Is.EqualTo(Kategorie.Abschlag));
        Assert.That(vorschlag.Klassifizierung.Dringlichkeit, Is.EqualTo(Dringlichkeit.Mittel));
        Assert.That(vorschlag.Klassifizierung.Konfidenz.Wert, Is.EqualTo(0.82).Within(1e-9));
        Assert.That(vorschlag.Klassifizierung.Begruendung, Is.EqualTo("Begründung der KI"));
        Assert.That(vorschlag.Klassifizierung.Antwortvorschlag, Is.EqualTo("Antwortvorschlag der KI"));
    }

    [Test]
    public async Task Das_Modell_kommt_aus_der_Antwort_nicht_aus_der_Konfiguration()
    {
        var klassifizierer = Klassifizierer(
            FakeHttpHandler.Antwortet(Antwort(Einordnung(), modell: "claude-haiku-4-5-ersatz")));

        var vorschlag = await klassifizierer.EinordnenAsync(NeuesTicket());

        Assert.That(vorschlag.Modell, Is.EqualTo("claude-haiku-4-5-ersatz"));
    }

    [Test]
    public async Task Ein_vorangestellter_Denkblock_wird_uebersprungen()
    {
        var klassifizierer = Klassifizierer(
            FakeHttpHandler.Antwortet(Antwort(Einordnung(), mitDenkblock: true)));

        var vorschlag = await klassifizierer.EinordnenAsync(NeuesTicket());

        Assert.That(vorschlag.Klassifizierung.Kategorie, Is.EqualTo(Kategorie.Abschlag));
    }

    [Test]
    public async Task Die_Anfrage_enthaelt_Modell_Tokengrenze_und_den_Tickettext()
    {
        var handler = FakeHttpHandler.Antwortet(Antwort(Einordnung()));
        var klassifizierer = Klassifizierer(handler);

        await klassifizierer.EinordnenAsync(NeuesTicket("Im Keller riecht es nach Gas."));

        Assert.That(handler.LetzterRumpf, Does.Contain(KiModell));
        Assert.That(handler.LetzterRumpf, Does.Contain("2048"));
        Assert.That(handler.LetzterRumpf, Does.Contain("Im Keller riecht es nach Gas."));
    }

    [Test]
    public void Kaputtes_JSON_im_Textblock_ist_keine_Einordnung()
    {
        var klassifizierer = Klassifizierer(FakeHttpHandler.Antwortet(Antwort("{\"kategorie\":")));

        Assert.That(async () => await klassifizierer.EinordnenAsync(NeuesTicket()),
                    Throws.InstanceOf<KlassifizierungNichtMoeglich>());
    }

    [TestCase(1.4)]
    [TestCase(-0.2)]
    public void Eine_Konfidenz_ausserhalb_von_0_bis_1_ist_keine_Einordnung(double konfidenz)
    {
        var klassifizierer = Klassifizierer(
            FakeHttpHandler.Antwortet(Antwort(Einordnung(konfidenz: konfidenz))));

        Assert.That(async () => await klassifizierer.EinordnenAsync(NeuesTicket()),
                    Throws.InstanceOf<KlassifizierungNichtMoeglich>());
    }

    [TestCase("Quatsch")]
    [TestCase("1")]
    public void Eine_unbekannte_Kategorie_ist_keine_Einordnung(string kategorie)
    {
        var klassifizierer = Klassifizierer(
            FakeHttpHandler.Antwortet(Antwort(Einordnung(kategorie: kategorie))));

        Assert.That(async () => await klassifizierer.EinordnenAsync(NeuesTicket()),
                    Throws.InstanceOf<KlassifizierungNichtMoeglich>());
    }

    [Test]
    public void Eine_unbekannte_Dringlichkeit_ist_keine_Einordnung()
    {
        var klassifizierer = Klassifizierer(
            FakeHttpHandler.Antwortet(Antwort(Einordnung(dringlichkeit: "Sehrdringend"))));

        Assert.That(async () => await klassifizierer.EinordnenAsync(NeuesTicket()),
                    Throws.InstanceOf<KlassifizierungNichtMoeglich>());
    }

    [TestCase(HttpStatusCode.Unauthorized)]
    [TestCase(HttpStatusCode.NotFound)]
    [TestCase(HttpStatusCode.TooManyRequests)]
    [TestCase(HttpStatusCode.InternalServerError)]
    [TestCase((HttpStatusCode)529)]
    public void Ein_Fehlerstatus_ist_keine_Einordnung(HttpStatusCode status)
    {
        var klassifizierer = Klassifizierer(FakeHttpHandler.Scheitert(status));

        Assert.That(async () => await klassifizierer.EinordnenAsync(NeuesTicket()),
                    Throws.InstanceOf<KlassifizierungNichtMoeglich>());
    }

    [TestCase("refusal")]
    [TestCase("max_tokens")]
    public void Ein_unbrauchbarer_Abbruchgrund_ist_keine_Einordnung(string stopGrund)
    {
        var klassifizierer = Klassifizierer(
            FakeHttpHandler.Antwortet(Antwort(Einordnung(), stopGrund: stopGrund)));

        Assert.That(async () => await klassifizierer.EinordnenAsync(NeuesTicket()),
                    Throws.InstanceOf<KlassifizierungNichtMoeglich>());
    }

    [Test]
    public void Eine_Zeitueberschreitung_ist_keine_Einordnung()
    {
        var klassifizierer = Klassifizierer(FakeHttpHandler.LaeuftInDieZeitueberschreitung());

        Assert.That(async () => await klassifizierer.EinordnenAsync(NeuesTicket()),
                    Throws.InstanceOf<KlassifizierungNichtMoeglich>());
    }

    [Test]
    public void Ein_Abbruch_durch_den_Aufrufer_bleibt_ein_Abbruch()
    {
        var klassifizierer = Klassifizierer(FakeHttpHandler.Antwortet(Antwort(Einordnung())));
        using var quelle = new CancellationTokenSource();
        quelle.Cancel();

        Assert.That(async () => await klassifizierer.EinordnenAsync(NeuesTicket(), quelle.Token),
                    Throws.InstanceOf<OperationCanceledException>()
                          .And.Not.InstanceOf<KlassifizierungNichtMoeglich>());
    }

    [Test]
    public async Task Ohne_Aufwand_enthaelt_die_Anfrage_kein_effort_Feld()
    {
        var handler = FakeHttpHandler.Antwortet(Antwort(Einordnung()));
        var klassifizierer = Klassifizierer(handler, aufwand: null);

        await klassifizierer.EinordnenAsync(NeuesTicket());

        Assert.That(handler.LetzterRumpf, Does.Not.Contain("effort"));
    }

    [Test]
    public async Task Mit_Aufwand_enthaelt_die_Anfrage_das_effort_Feld()
    {
        var handler = FakeHttpHandler.Antwortet(Antwort(Einordnung()));
        var klassifizierer = Klassifizierer(handler, aufwand: "high");

        await klassifizierer.EinordnenAsync(NeuesTicket());

        Assert.That(handler.LetzterRumpf, Does.Contain("effort"));
        Assert.That(handler.LetzterRumpf, Does.Contain("high"));
    }
}