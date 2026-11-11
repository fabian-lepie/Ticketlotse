using NUnit.Framework;
using Ticketlotse.Application;
using Ticketlotse.Domain;
using Ticketlotse.Infrastructure;

namespace Ticketlotse.Tests;

public class TicketverwaltungTests
{
    private ITicketRepository _repository = null!;
    private Ticketverwaltung _verwaltung = null!;

    [SetUp]
    public void SetUp()
    {
        _repository = new TicketRepositoryImArbeitsspeicher();
        _verwaltung = new Ticketverwaltung(_repository);
    }

    private Task<Ticket> Erfassen(string text = "Mein Abschlag ist zu hoch.") =>
        _verwaltung.ErfassenAsync("kunde@example.de", "Abschlag", text);

    private static Klassifizierung Vorschlag() =>
        new(Kategorie.Abschlag, Dringlichkeit.Mittel, new Konfidenz(0.9), "Begründung", "Antwortvorschlag");

    [Test]
    public async Task Ein_erfasstes_Ticket_ist_danach_auffindbar()
    {
        var ticket = await Erfassen();

        var gefunden = await _verwaltung.FindenAsync(ticket.Id);

        Assert.That(gefunden, Is.SameAs(ticket));
        Assert.That(gefunden!.Status, Is.EqualTo(TicketStatus.Neu));
    }

    [Test]
    public async Task Unbekannte_Id_liefert_kein_Ticket()
    {
        Assert.That(await _verwaltung.FindenAsync(Guid.NewGuid()), Is.Null);
    }

    [Test]
    public async Task Suchen_ohne_Status_gibt_alle_Tickets()
    {
        await Erfassen();
        await Erfassen("Bitte Zählerstand korrigieren.");

        Assert.That(await _verwaltung.SuchenAsync(), Has.Count.EqualTo(2));
    }

    [Test]
    public async Task Suchen_filtert_nach_Status()
    {
        var neu = await Erfassen();
        var eingeordnet = await Erfassen("Bitte Zählerstand korrigieren.");
        eingeordnet.Einordnen(Vorschlag());
        await _repository.AktualisierenAsync(eingeordnet);

        var wartend = await _verwaltung.SuchenAsync(TicketStatus.WartetAufFreigabe);

        Assert.That(wartend.Select(t => t.Id), Is.EquivalentTo(new[] { eingeordnet.Id }));
        Assert.That(wartend.Select(t => t.Id), Does.Not.Contain(neu.Id));
    }

    [Test]
    public async Task Ein_eingeordnetes_Ticket_wird_freigegeben()
    {
        var ticket = await Erfassen();
        ticket.Einordnen(Vorschlag());
        await _repository.AktualisierenAsync(ticket);

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
        var ticket = await Erfassen();

        Assert.That(async () => await _verwaltung.FreigebenAsync(ticket.Id),
                    Throws.InvalidOperationException);
    }
}
