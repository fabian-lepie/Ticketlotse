using NUnit.Framework;
using Ticketlotse.Domain;

namespace Ticketlotse.Tests;

public class TicketTests
{
    private static Ticket NeuesTicket(string text = "Bitte senken Sie meinen Abschlag.") =>
        Ticket.Erstellen("kunde@example.de", "Anfrage", text);

    private static Klassifizierung Vorschlag(
        Kategorie kategorie = Kategorie.Abschlag,
        Dringlichkeit dringlichkeit = Dringlichkeit.Niedrig,
        double sicherheit = 0.9) =>
        new(kategorie, dringlichkeit, new Sicherheit(sicherheit), "Begründung", "Antwortvorschlag");

    [Test]
    public void Neues_Ticket_hat_Status_Neu()
    {
        Assert.That(NeuesTicket().Status, Is.EqualTo(TicketStatus.Neu));
    }

    [TestCase("")]
    [TestCase("   ")]
    public void Ticket_ohne_Text_ist_nicht_erlaubt(string text)
    {
        Assert.That(() => Ticket.Erstellen("kunde@example.de", "Anfrage", text),
                    Throws.InstanceOf<ArgumentException>());
    }

    [Test]
    public void Bei_ausreichender_Sicherheit_wartet_das_Ticket_auf_Freigabe()
    {
        var ticket = NeuesTicket();
        ticket.Einordnen(Vorschlag(sicherheit: 0.9));

        Assert.That(ticket.Status, Is.EqualTo(TicketStatus.WartetAufFreigabe));
    }

    [Test]
    public void Bei_geringer_Sicherheit_geht_das_Ticket_in_die_manuelle_Pruefung()
    {
        var ticket = NeuesTicket();
        ticket.Einordnen(Vorschlag(sicherheit: 0.5));

        Assert.That(ticket.Status, Is.EqualTo(TicketStatus.ManuellePruefung));
    }

    [TestCase("Im Treppenhaus ist starker Gasgeruch!")]
    [TestCase("Seit heute früh Stromausfall in der ganzen Straße.")]
    [TestCase("Wir haben seit gestern kein Wasser mehr.")]
    public void Notfall_ist_immer_hoch_dringlich_auch_wenn_die_KI_etwas_anderes_sagt(string text)
    {
        var ticket = NeuesTicket(text);
        ticket.Einordnen(Vorschlag(Kategorie.Sonstiges, Dringlichkeit.Niedrig));

        Assert.That(ticket.Klassifizierung!.Dringlichkeit, Is.EqualTo(Dringlichkeit.Hoch));
    }

    [Test]
    public void Ohne_Notfall_bleibt_die_Dringlichkeit_des_Vorschlags()
    {
        var ticket = NeuesTicket();
        ticket.Einordnen(Vorschlag(dringlichkeit: Dringlichkeit.Mittel));

        Assert.That(ticket.Klassifizierung!.Dringlichkeit, Is.EqualTo(Dringlichkeit.Mittel));
    }

    [Test]
    public void Das_Team_ergibt_sich_aus_der_Kategorie()
    {
        var ticket = NeuesTicket();
        ticket.Einordnen(Vorschlag(Kategorie.Zaehlerstand));

        Assert.That(ticket.Team, Is.EqualTo(Team.Messwesen));
    }

    [Test]
    public void Ein_Ticket_kann_nur_einmal_eingeordnet_werden()
    {
        var ticket = NeuesTicket();
        ticket.Einordnen(Vorschlag());

        Assert.That(() => ticket.Einordnen(Vorschlag()), Throws.InvalidOperationException);
    }

    [Test]
    public void Freigabe_vor_der_Einordnung_ist_nicht_erlaubt()
    {
        Assert.That(() => NeuesTicket().Freigeben(), Throws.InvalidOperationException);
    }

    [Test]
    public void Ein_eingeordnetes_Ticket_kann_freigegeben_werden()
    {
        var ticket = NeuesTicket();
        ticket.Einordnen(Vorschlag());
        ticket.Freigeben();

        Assert.That(ticket.Status, Is.EqualTo(TicketStatus.Freigegeben));
    }
}
