using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using Ticketlotse.Application;
using Ticketlotse.Domain;
using Ticketlotse.Infrastructure;

namespace Ticketlotse.Tests;

public class TicketverwaltungTests
{
    private const string KiModell = "test-modell";

    private ITicketRepository _repository = null!;
    private FakeKlassifizierer _ki = null!;
    private FakeKlassifizierer _kaputteKi = null!;

    // Beide Verwaltungen teilen sich dieselbe Ablage – ein Ticket, das ohne KI
    // angelegt wurde, lässt sich dadurch später mit funktionierender KI einordnen.
    private Ticketverwaltung _verwaltung = null!;
    private Ticketverwaltung _ohneKi = null!;

    [SetUp]
    public void SetUp()
    {
        _repository = new TicketRepositoryImArbeitsspeicher();
        _ki = FakeKlassifizierer.Liefert(modell: KiModell);
        _kaputteKi = FakeKlassifizierer.Scheitert();
        _verwaltung = new Ticketverwaltung(_repository, _ki, NullLogger<Ticketverwaltung>.Instance);
        _ohneKi = new Ticketverwaltung(_repository, _kaputteKi, NullLogger<Ticketverwaltung>.Instance);
    }

    private static Task<Ticket> Erfassen(Ticketverwaltung verwaltung,
                                         string text = "Mein Abschlag ist zu hoch.") =>
        verwaltung.ErfassenAsync("kunde@example.de", "Abschlag", text);

    [Test]
    public async Task Ein_erfasstes_Ticket_ist_danach_auffindbar()
    {
        var ticket = await Erfassen(_verwaltung);

        Assert.That(await _verwaltung.FindenAsync(ticket.Id), Is.SameAs(ticket));
    }

    [Test]
    public async Task Unbekannte_Id_liefert_kein_Ticket()
    {
        Assert.That(await _verwaltung.FindenAsync(Guid.NewGuid()), Is.Null);
    }

    [Test]
    public async Task Ein_erfasstes_Ticket_wird_sofort_von_der_KI_eingeordnet()
    {
        var ticket = await Erfassen(_verwaltung);

        Assert.That(ticket.Status, Is.EqualTo(TicketStatus.WartetAufFreigabe));
        Assert.That(ticket.EinordnungVerlauf, Has.Count.EqualTo(1));
        Assert.That(ticket.AktuelleEinordnung!.Urheber, Is.EqualTo(Urheber.KI));
        Assert.That(ticket.AktuelleEinordnung.Bearbeiter, Is.EqualTo(KiModell));
    }

    [Test]
    public async Task Bei_geringer_Konfidenz_geht_das_erfasste_Ticket_in_die_manuelle_Pruefung()
    {
        var verwaltung = new Ticketverwaltung(
            _repository, FakeKlassifizierer.Liefert(konfidenz: 0.4), NullLogger<Ticketverwaltung>.Instance);

        var ticket = await Erfassen(verwaltung);

        Assert.That(ticket.Status, Is.EqualTo(TicketStatus.ManuellePruefung));
    }

    [Test]
    public async Task Faellt_die_KI_aus_bleibt_das_Ticket_erhalten_und_neu()
    {
        var ticket = await Erfassen(_ohneKi);

        Assert.That(ticket.Status, Is.EqualTo(TicketStatus.Neu));
        Assert.That(ticket.EinordnungVerlauf, Is.Empty);
        Assert.That(await _ohneKi.FindenAsync(ticket.Id), Is.Not.Null, "Das Ticket darf nicht verlorengehen.");
    }

    [Test]
    public async Task Suchen_ohne_Status_gibt_alle_Tickets()
    {
        await Erfassen(_verwaltung);
        await Erfassen(_ohneKi, "Bitte Zählerstand korrigieren.");

        Assert.That(await _verwaltung.SuchenAsync(), Has.Count.EqualTo(2));
    }

    [Test]
    public async Task Suchen_filtert_nach_Status()
    {
        var liegengeblieben = await Erfassen(_ohneKi);
        var eingeordnet = await Erfassen(_verwaltung, "Bitte Zählerstand korrigieren.");

        var wartend = await _verwaltung.SuchenAsync(TicketStatus.WartetAufFreigabe);

        Assert.That(wartend.Select(t => t.Id), Is.EquivalentTo(new[] { eingeordnet.Id }));
        Assert.That(wartend.Select(t => t.Id), Does.Not.Contain(liegengeblieben.Id));
    }

    [Test]
    public async Task Ein_liegengebliebenes_Ticket_kann_nachtraeglich_eingeordnet_werden()
    {
        var ticket = await Erfassen(_ohneKi);

        var eingeordnet = await _verwaltung.ErneutEinordnenAsync(ticket.Id);

        Assert.That(eingeordnet!.Status, Is.EqualTo(TicketStatus.WartetAufFreigabe));
        Assert.That(eingeordnet.AktuelleEinordnung!.Bearbeiter, Is.EqualTo(KiModell));
        Assert.That(_ki.Aufrufe, Is.EqualTo(1), "Die KI muss dafür tatsächlich gefragt worden sein.");
    }

    [Test]
    public async Task Ein_bereits_eingeordnetes_Ticket_kann_nicht_erneut_eingeordnet_werden()
    {
        var ticket = await Erfassen(_verwaltung);

        Assert.That(async () => await _verwaltung.ErneutEinordnenAsync(ticket.Id),
                    Throws.InvalidOperationException);
    }

    [Test]
    public async Task Erneutes_Einordnen_einer_unbekannten_Id_liefert_kein_Ticket()
    {
        Assert.That(await _verwaltung.ErneutEinordnenAsync(Guid.NewGuid()), Is.Null);
    }

    [Test]
    public async Task Eine_Korrektur_wird_als_menschliche_Einordnung_angehaengt()
    {
        var ticket = await Erfassen(_verwaltung);

        var korrigiert = await _verwaltung.KorrigierenAsync(
            ticket.Id, Kategorie.Lieferantenwechsel, Dringlichkeit.Mittel,
            "Kunde will wechseln.", "m.schmidt");

        Assert.That(korrigiert!.EinordnungVerlauf, Has.Count.EqualTo(2));
        Assert.That(korrigiert.AktuelleEinordnung!.Urheber, Is.EqualTo(Urheber.Mensch));
        Assert.That(korrigiert.AktuelleEinordnung.Bearbeiter, Is.EqualTo("m.schmidt"));
        Assert.That(korrigiert.Team, Is.EqualTo(Team.Vertrieb));
        Assert.That(korrigiert.Status, Is.EqualTo(TicketStatus.WartetAufFreigabe));
    }

    [Test]
    public async Task Korrektur_einer_unbekannten_Id_liefert_kein_Ticket()
    {
        Assert.That(await _verwaltung.KorrigierenAsync(
                        Guid.NewGuid(), Kategorie.Umzug, Dringlichkeit.Mittel, "Grund", "m.schmidt"),
                    Is.Null);
    }

    [Test]
    public async Task Ein_eingeordnetes_Ticket_wird_freigegeben()
    {
        var ticket = await Erfassen(_verwaltung);

        var freigegeben = await _verwaltung.FreigebenAsync(ticket.Id);

        Assert.That(freigegeben!.Status, Is.EqualTo(TicketStatus.Freigegeben));
    }

    [Test]
    public async Task Freigabe_eines_unbekannten_Tickets_liefert_kein_Ticket()
    {
        Assert.That(await _verwaltung.FreigebenAsync(Guid.NewGuid()), Is.Null);
    }

    [Test]
    public async Task Ein_noch_nicht_eingeordnetes_Ticket_kann_nicht_freigegeben_werden()
    {
        var ticket = await Erfassen(_ohneKi);

        Assert.That(async () => await _verwaltung.FreigebenAsync(ticket.Id),
                    Throws.InvalidOperationException);
    }
}