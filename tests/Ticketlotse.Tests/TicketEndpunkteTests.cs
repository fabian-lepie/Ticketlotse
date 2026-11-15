using System.Net;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Ticketlotse.Api;
using Ticketlotse.Application;
using Ticketlotse.Domain;
using Ticketlotse.Infrastructure;


namespace Ticketlotse.Tests;

public class TicketEndpunkteTests
{
    // Jeder Test bekommt eine frische Anwendung – der Speicher ist damit leer.
    private WebApplicationFactory<Program> _anwendung = null!;
    private HttpClient _client = null!;

    [SetUp]
    public void SetUp()
    {
        // Ausdrücklich ohne KI: kein Test darf von einem konfigurierten
        // Zugangsschlüssel abhängen oder echte Anfragen verschicken.
        _anwendung = new WebApplicationFactory<Program>().WithWebHostBuilder(aufbau =>
            aufbau.ConfigureServices(dienste =>
                dienste.AddSingleton<ITicketKlassifizierer, KeineKiAngebunden>()));

        _client = _anwendung.CreateClient();
    }

    [TearDown]
    public void TearDown()
    {
        _client.Dispose();
        _anwendung.Dispose();
    }

    private static TicketAnlegenAnfrage Anfrage(string text = "Mein Abschlag ist zu hoch.") =>
        new("kunde@example.de", "Abschlag", text);

    private async Task<TicketAntwort> TicketAnlegen(string text = "Mein Abschlag ist zu hoch.")
    {
        var antwort = await _client.PostAsJsonAsync("/tickets", Anfrage(text));
        antwort.EnsureSuccessStatusCode();
        return (await antwort.Content.ReadFromJsonAsync<TicketAntwort>())!;
    }

    [Test]
    public async Task POST_tickets_legt_ein_neues_Ticket_an()
    {
        var antwort = await _client.PostAsJsonAsync("/tickets", Anfrage());

        Assert.That(antwort.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        var ticket = (await antwort.Content.ReadFromJsonAsync<TicketAntwort>())!;
        Assert.That(ticket.Id, Is.Not.EqualTo(Guid.Empty));
        Assert.That(ticket.Status, Is.EqualTo(nameof(TicketStatus.Neu)));
        Assert.That(ticket.Kategorie, Is.Null, "Die Einordnung kommt erst später durch die KI.");
        Assert.That(antwort.Headers.Location!.ToString(), Does.Contain(ticket.Id.ToString()));
    }

    [Test]
    public async Task POST_tickets_ohne_Text_wird_abgelehnt()
    {
        var antwort = await _client.PostAsJsonAsync("/tickets", Anfrage(text: "   "));

        Assert.That(antwort.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task GET_ticket_gibt_das_angelegte_Ticket_zurueck()
    {
        var angelegt = await TicketAnlegen();

        var ticket = await _client.GetFromJsonAsync<TicketAntwort>($"/tickets/{angelegt.Id}");

        Assert.That(ticket!.Id, Is.EqualTo(angelegt.Id));
        Assert.That(ticket.Absender, Is.EqualTo("kunde@example.de"));
    }

    [Test]
    public async Task GET_unbekanntes_Ticket_gibt_404()
    {
        var antwort = await _client.GetAsync($"/tickets/{Guid.NewGuid()}");

        Assert.That(antwort.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task GET_tickets_filtert_nach_Status()
    {
        var angelegt = await TicketAnlegen();

        var neue = await _client.GetFromJsonAsync<TicketAntwort[]>("/tickets?status=Neu");
        var freigegebene = await _client.GetFromJsonAsync<TicketAntwort[]>("/tickets?status=Freigegeben");

        Assert.That(neue!.Select(t => t.Id), Is.EquivalentTo(new[] { angelegt.Id }));
        Assert.That(freigegebene, Is.Empty);
    }

    [Test]
    public async Task GET_tickets_mit_unbekanntem_Status_wird_abgelehnt()
    {
        var antwort = await _client.GetAsync("/tickets?status=Quatsch");

        Assert.That(antwort.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [TestCase("99")]
    [TestCase("2")]
    public async Task GET_tickets_mit_Status_als_Zahl_wird_abgelehnt(string status)
    {
        var antwort = await _client.GetAsync($"/tickets?status={status}");

        Assert.That(antwort.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [TestCase("neu")]
    [TestCase("NEU")]
    public async Task GET_tickets_akzeptiert_den_Status_unabhaengig_von_Gross_und_Kleinschreibung(string status)
    {
        var antwort = await _client.GetAsync($"/tickets?status={status}");

        Assert.That(antwort.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task PUT_freigabe_eines_unbekannten_Tickets_gibt_404()
    {
        var antwort = await _client.PutAsync($"/tickets/{Guid.NewGuid()}/freigabe", null);

        Assert.That(antwort.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task PUT_freigabe_vor_der_Einordnung_gibt_409()
    {
        var angelegt = await TicketAnlegen();

        var antwort = await _client.PutAsync($"/tickets/{angelegt.Id}/freigabe", null);

        Assert.That(antwort.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
    }

    [Test]
    public async Task POST_tickets_mit_kaputtem_JSON_wird_abgelehnt()
    {
        using var inhalt = new StringContent("{\"absender\": ", Encoding.UTF8, "application/json");

        var antwort = await _client.PostAsync("/tickets", inhalt);

        Assert.That(antwort.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task POST_tickets_ohne_Body_wird_abgelehnt()
    {
        var antwort = await _client.PostAsync("/tickets", null);

        Assert.That(antwort.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }


    [Test]
    public async Task POST_tickets_leerer_Body_wird_abgelehnt()
    {
        using var inhalt = new StringContent("", Encoding.UTF8, "application/json");

        var antwort = await _client.PostAsync("/tickets", inhalt);

        Assert.That(antwort.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        // keine Fehlerdetails in der Antwort
        var koerper = await antwort.Content.ReadAsStringAsync();
        Assert.That(koerper, Does.Not.Contain("System.Text.Json"));
    }

    [Test]
    public async Task POST_tickets_mit_falschem_Content_Type_wird_abgelehnt()
    {
        using var inhalt = new StringContent("Hallo", Encoding.UTF8, "text/plain");

        var antwort = await _client.PostAsync("/tickets", inhalt);

        Assert.That(antwort.StatusCode, Is.EqualTo(HttpStatusCode.UnsupportedMediaType));
    }

    [Test]
    public async Task GET_ticket_mit_einer_Id_die_keine_Guid_ist_gibt_404()
    {
        var antwort = await _client.GetAsync("/tickets/abc");

        Assert.That(antwort.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task PUT_freigabe_mit_einer_Id_die_keine_Guid_ist_gibt_404()
    {
        var antwort = await _client.PutAsync("/tickets/abc/freigabe", null);

        Assert.That(antwort.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    // Überschreibt die in Program.cs registrierte KeineKiAngebunden – die letzte Registrierung gewinnt.
    private WebApplicationFactory<Program> MitKi(ITicketKlassifizierer klassifizierer) =>
        _anwendung.WithWebHostBuilder(aufbau =>
            aufbau.ConfigureServices(dienste => dienste.AddSingleton(klassifizierer)));

    private static async Task<TicketAntwort> TicketAnlegen(
        HttpClient client, string text = "Mein Abschlag ist zu hoch.")
    {
        var antwort = await client.PostAsJsonAsync(
            "/tickets", new TicketAnlegenAnfrage("kunde@example.de", "Abschlag", text));
        antwort.EnsureSuccessStatusCode();
        return (await antwort.Content.ReadFromJsonAsync<TicketAntwort>())!;
    }

    private static KorrekturAnfrage Korrektur(
        string kategorie = "Lieferantenwechsel",
        string dringlichkeit = "Mittel",
        string begruendung = "Kunde will wechseln, keine Rechnungsfrage.",
        string bearbeiter = "m.schmidt") => new(kategorie, dringlichkeit, begruendung, bearbeiter);

    [Test]
    public async Task POST_tickets_ordnet_das_Ticket_sofort_ein()
    {
        using var anwendung = MitKi(FakeKlassifizierer.Liefert(Kategorie.Zaehlerstand));
        using var client = anwendung.CreateClient();

        var ticket = await TicketAnlegen(client);

        Assert.That(ticket.Status, Is.EqualTo(nameof(TicketStatus.WartetAufFreigabe)));
        Assert.That(ticket.Kategorie, Is.EqualTo(nameof(Kategorie.Zaehlerstand)));
        Assert.That(ticket.Team, Is.EqualTo(nameof(Team.Messwesen)));
        Assert.That(ticket.Konfidenz, Is.EqualTo(0.9));
    }

    [Test]
    public async Task Der_vollstaendige_Weg_von_der_Anlage_bis_zur_Freigabe()
    {
        using var anwendung = MitKi(FakeKlassifizierer.Liefert());
        using var client = anwendung.CreateClient();
        var angelegt = await TicketAnlegen(client);

        var antwort = await client.PutAsync($"/tickets/{angelegt.Id}/freigabe", null);
        var freigegeben = (await antwort.Content.ReadFromJsonAsync<TicketAntwort>())!;

        Assert.That(antwort.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(freigegeben.Status, Is.EqualTo(nameof(TicketStatus.Freigegeben)));
    }

    [Test]
    public async Task POST_einordnung_holt_eine_ausgefallene_Einordnung_nach()
    {
        using var anwendung = MitKi(FakeKlassifizierer.ScheitertBeimErstenMal());
        using var client = anwendung.CreateClient();
        var angelegt = await TicketAnlegen(client);
        Assert.That(angelegt.Status, Is.EqualTo(nameof(TicketStatus.Neu)), "Der erste Versuch muss scheitern.");

        var antwort = await client.PostAsync($"/tickets/{angelegt.Id}/einordnung", null);
        var eingeordnet = (await antwort.Content.ReadFromJsonAsync<TicketAntwort>())!;

        Assert.That(antwort.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(eingeordnet.Status, Is.EqualTo(nameof(TicketStatus.WartetAufFreigabe)));
    }

    [Test]
    public async Task POST_einordnung_auf_ein_bereits_eingeordnetes_Ticket_gibt_409()
    {
        using var anwendung = MitKi(FakeKlassifizierer.Liefert());
        using var client = anwendung.CreateClient();
        var angelegt = await TicketAnlegen(client);

        var antwort = await client.PostAsync($"/tickets/{angelegt.Id}/einordnung", null);

        Assert.That(antwort.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
    }

    [Test]
    public async Task POST_einordnung_gibt_503_wenn_die_KI_weiter_ausfaellt()
    {
        using var anwendung = MitKi(FakeKlassifizierer.Scheitert());
        using var client = anwendung.CreateClient();
        var angelegt = await TicketAnlegen(client);

        var antwort = await client.PostAsync($"/tickets/{angelegt.Id}/einordnung", null);

        Assert.That(antwort.StatusCode, Is.EqualTo(HttpStatusCode.ServiceUnavailable));
    }

    [Test]
    public async Task POST_einordnung_auf_eine_unbekannte_Id_gibt_404()
    {
        var antwort = await _client.PostAsync($"/tickets/{Guid.NewGuid()}/einordnung", null);

        Assert.That(antwort.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task PUT_klassifizierung_haengt_eine_menschliche_Korrektur_an()
    {
        using var anwendung = MitKi(FakeKlassifizierer.Liefert(Kategorie.Rechnung));
        using var client = anwendung.CreateClient();
        var angelegt = await TicketAnlegen(client);

        var antwort = await client.PutAsJsonAsync($"/tickets/{angelegt.Id}/klassifizierung", Korrektur());
        var korrigiert = (await antwort.Content.ReadFromJsonAsync<TicketAntwort>())!;

        Assert.That(antwort.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(korrigiert.Kategorie, Is.EqualTo(nameof(Kategorie.Lieferantenwechsel)));
        Assert.That(korrigiert.Team, Is.EqualTo(nameof(Team.Vertrieb)));
        Assert.That(korrigiert.Status, Is.EqualTo(nameof(TicketStatus.WartetAufFreigabe)));
        Assert.That(korrigiert.Konfidenz, Is.Null, "Eine menschliche Einordnung hat keine Konfidenz.");
        Assert.That(korrigiert.Antwortvorschlag, Is.Null);
    }

    [Test]
    public async Task GET_ticket_liefert_den_Verlauf_mit_KI_Vorschlag_und_Korrektur()
    {
        using var anwendung = MitKi(FakeKlassifizierer.Liefert(Kategorie.Rechnung));
        using var client = anwendung.CreateClient();
        var angelegt = await TicketAnlegen(client);
        await client.PutAsJsonAsync($"/tickets/{angelegt.Id}/klassifizierung", Korrektur());

        var detail = await client.GetFromJsonAsync<TicketDetailAntwort>($"/tickets/{angelegt.Id}");

        Assert.That(detail!.Verlauf, Has.Count.EqualTo(2));
        Assert.That(detail.Verlauf[0].Urheber, Is.EqualTo(nameof(Urheber.KI)));
        Assert.That(detail.Verlauf[0].Kategorie, Is.EqualTo(nameof(Kategorie.Rechnung)));
        Assert.That(detail.Verlauf[0].Bearbeiter, Is.EqualTo("test-modell"));
        Assert.That(detail.Verlauf[1].Urheber, Is.EqualTo(nameof(Urheber.Mensch)));
        Assert.That(detail.Verlauf[1].Bearbeiter, Is.EqualTo("m.schmidt"));
        Assert.That(detail.Verlauf[1].Antwortvorschlag, Is.Null);
    }

    [TestCase("Quatsch", "Mittel")]
    [TestCase("Rechnung", "Sehrdringend")]
    [TestCase("2", "Mittel")]
    public async Task PUT_klassifizierung_mit_unbekannten_Werten_wird_abgelehnt(
        string kategorie, string dringlichkeit)
    {
        using var anwendung = MitKi(FakeKlassifizierer.Liefert());
        using var client = anwendung.CreateClient();
        var angelegt = await TicketAnlegen(client);

        var antwort = await client.PutAsJsonAsync($"/tickets/{angelegt.Id}/klassifizierung",
                                                  Korrektur(kategorie, dringlichkeit));

        Assert.That(antwort.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task PUT_klassifizierung_ohne_Begruendung_wird_abgelehnt()
    {
        using var anwendung = MitKi(FakeKlassifizierer.Liefert());
        using var client = anwendung.CreateClient();
        var angelegt = await TicketAnlegen(client);

        var antwort = await client.PutAsJsonAsync($"/tickets/{angelegt.Id}/klassifizierung",
                                                  Korrektur(begruendung: "  "));

        Assert.That(antwort.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task PUT_klassifizierung_nach_der_Freigabe_gibt_409()
    {
        using var anwendung = MitKi(FakeKlassifizierer.Liefert());
        using var client = anwendung.CreateClient();
        var angelegt = await TicketAnlegen(client);
        await client.PutAsync($"/tickets/{angelegt.Id}/freigabe", null);

        var antwort = await client.PutAsJsonAsync($"/tickets/{angelegt.Id}/klassifizierung", Korrektur());

        Assert.That(antwort.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
    }

    [Test]
    public async Task PUT_klassifizierung_auf_eine_unbekannte_Id_gibt_404()
    {
        var antwort = await _client.PutAsJsonAsync($"/tickets/{Guid.NewGuid()}/klassifizierung", Korrektur());

        Assert.That(antwort.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }


    private sealed class StolperndesRepository : ITicketRepository
    {
        public Task HinzufuegenAsync(Ticket ticket, CancellationToken abbruch = default) =>
            throw new IOException("Datenspeicher nicht erreichbar");

        public Task<Ticket?> FindenAsync(Guid id, CancellationToken abbruch = default) =>
            throw new IOException("Datenspeicher nicht erreichbar");

        public Task<IReadOnlyList<Ticket>> SuchenAsync(TicketStatus? status = null, CancellationToken abbruch = default) =>
            throw new IOException("Datenspeicher nicht erreichbar");

        public Task AktualisierenAsync(Ticket ticket, CancellationToken abbruch = default) =>
            throw new IOException("Datenspeicher nicht erreichbar");
    }

    [Test]
    public async Task Unerwartete_Fehler_werden_als_500_ohne_Interna_beantwortet()
    {
        using var anwendung = _anwendung.WithWebHostBuilder(aufbau =>
            aufbau.ConfigureServices(dienste =>
                dienste.AddSingleton<ITicketRepository, StolperndesRepository>()));
        using var client = anwendung.CreateClient();

        var antwort = await client.PostAsJsonAsync("/tickets", Anfrage());
        var koerper = await antwort.Content.ReadAsStringAsync();

        Assert.That(antwort.StatusCode, Is.EqualTo(HttpStatusCode.InternalServerError));
        Assert.That(koerper, Does.Not.Contain("StolperndesRepository"));
        Assert.That(koerper, Does.Not.Contain("Datenspeicher nicht erreichbar"));
    }

    private sealed class ProtokollSammler : ILoggerProvider
    {
        private readonly List<(LogLevel Level, string Kategorie)> _zeilen = [];

        public IReadOnlyList<(LogLevel Level, string Kategorie)> Zeilen
        {
            get { lock (_zeilen) return _zeilen.ToList(); }
        }

        public ILogger CreateLogger(string categoryName) => new Sammler(this, categoryName);

        public void Dispose() { }

        private sealed class Sammler(ProtokollSammler besitzer, string kategorie) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
                Exception? exception, Func<TState, Exception?, string> formatter)
            {
                lock (besitzer._zeilen) besitzer._zeilen.Add((logLevel, kategorie));
            }
        }
    }

    private async Task<IReadOnlyList<(LogLevel Level, string Kategorie)>> KaputtesJsonSenden(string umgebung)
    {
        var sammler = new ProtokollSammler();
        using var anwendung = _anwendung.WithWebHostBuilder(aufbau =>
        {
            aufbau.UseEnvironment(umgebung);
            aufbau.ConfigureLogging(protokoll => protokoll.AddProvider(sammler));
        });
        using var client = anwendung.CreateClient();
        using var inhalt = new StringContent("{\"absender\": ", Encoding.UTF8, "application/json");

        var antwort = await client.PostAsync("/tickets", inhalt);

        Assert.That(antwort.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest),
                    "Der Statuscode muss in jeder Umgebung gleich sein.");
        return sammler.Zeilen;
    }

    [Test]
    public async Task Ungueltige_Anfragen_werden_in_Development_protokolliert()
    {
        var zeilen = await KaputtesJsonSenden("Development");

        Assert.That(zeilen.Any(z => z.Level == LogLevel.Warning
                                 && z.Kategorie == "Ticketlotse.Api.UnerwarteteFehler"), Is.True);
    }

    [Test]
    public async Task Ungueltige_Anfragen_werden_in_Produktion_nicht_protokolliert()
    {
        var zeilen = await KaputtesJsonSenden("Production");

        Assert.That(zeilen.Any(z => z.Level >= LogLevel.Warning
                                 && z.Kategorie == "Ticketlotse.Api.UnerwarteteFehler"), Is.False);
    }


}
