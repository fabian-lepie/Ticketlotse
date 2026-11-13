using Ticketlotse.Domain;

namespace Ticketlotse.Tests;

public class TicketTests
{
    private const string KiModell = "test-modell";

    private static Ticket NeuesTicket(string text = "Bitte senken Sie meinen Abschlag.") =>
        Ticket.Erstellen("kunde@example.de", "Anfrage", text);

    private static Klassifizierung Vorschlag(
        Kategorie kategorie = Kategorie.Abschlag,
        Dringlichkeit dringlichkeit = Dringlichkeit.Niedrig,
        double konfidenz = 0.9) =>
        new(kategorie, dringlichkeit, new Konfidenz(konfidenz), "Begründung", "Antwortvorschlag");

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
    public void Bei_ausreichender_Konfidenz_wartet_das_Ticket_auf_Freigabe()
    {
        var ticket = NeuesTicket();
        ticket.Einordnen(Vorschlag(konfidenz: 0.9), KiModell);

        Assert.That(ticket.Status, Is.EqualTo(TicketStatus.WartetAufFreigabe));
    }

    [Test]
    public void Bei_geringer_Konfidenz_geht_das_Ticket_in_die_manuelle_Pruefung()
    {
        var ticket = NeuesTicket();
        ticket.Einordnen(Vorschlag(konfidenz: 0.5), KiModell);

        Assert.That(ticket.Status, Is.EqualTo(TicketStatus.ManuellePruefung));
    }

    [TestCase("Im Treppenhaus ist starker Gasgeruch!")]
    [TestCase("Seit heute früh Stromausfall in der ganzen Straße.")]
    [TestCase("Wir haben seit gestern kein Wasser mehr.")]
    public void Notfall_ist_immer_hoch_dringlich_auch_wenn_die_KI_etwas_anderes_sagt(string text)
    {
        var ticket = NeuesTicket(text);
        ticket.Einordnen(Vorschlag(Kategorie.Sonstiges, Dringlichkeit.Niedrig), KiModell);

        Assert.That(ticket.AktuelleEinordnung!.Dringlichkeit, Is.EqualTo(Dringlichkeit.Hoch));
    }

    [Test]
    public void Ohne_Notfall_bleibt_die_Dringlichkeit_des_Vorschlags()
    {
        var ticket = NeuesTicket();
        ticket.Einordnen(Vorschlag(dringlichkeit: Dringlichkeit.Mittel), KiModell);

        Assert.That(ticket.AktuelleEinordnung!.Dringlichkeit, Is.EqualTo(Dringlichkeit.Mittel));
    }

    [Test]
    public void Das_Team_ergibt_sich_aus_der_Kategorie()
    {
        var ticket = NeuesTicket();
        ticket.Einordnen(Vorschlag(Kategorie.Zaehlerstand), KiModell);

        Assert.That(ticket.Team, Is.EqualTo(Team.Messwesen));
    }

    [Test]
    public void Ein_Ticket_kann_nur_einmal_eingeordnet_werden()
    {
        var ticket = NeuesTicket();
        ticket.Einordnen(Vorschlag(), KiModell);

        Assert.That(() => ticket.Einordnen(Vorschlag(), KiModell), Throws.InvalidOperationException);
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
        ticket.Einordnen(Vorschlag(), KiModell);
        ticket.Freigeben();

        Assert.That(ticket.Status, Is.EqualTo(TicketStatus.Freigegeben));
    }

    [Test]
    public void Ein_neues_Ticket_hat_noch_keine_Einordnung()
    {
        var ticket = NeuesTicket();

        Assert.That(ticket.EinordnungVerlauf, Is.Empty);
        Assert.That(ticket.AktuelleEinordnung, Is.Null);
        Assert.That(ticket.Team, Is.Null);
    }

    [Test]
    public void Einordnen_erzeugt_einen_Eintrag_der_KI()
    {
        var ticket = NeuesTicket();
        ticket.Einordnen(Vorschlag(Kategorie.Rechnung), KiModell);

        Assert.That(ticket.EinordnungVerlauf, Has.Count.EqualTo(1));
        Assert.That(ticket.AktuelleEinordnung!.Urheber, Is.EqualTo(Urheber.KI));
        Assert.That(ticket.AktuelleEinordnung.Bearbeiter, Is.EqualTo(KiModell));
    }

    [Test]
    public void Der_Verlauf_behaelt_den_KI_Vorschlag_nach_einer_Korrektur()
    {
        var ticket = NeuesTicket();
        ticket.Einordnen(Vorschlag(Kategorie.Rechnung), KiModell);
        ticket.Korrigieren(Kategorie.Lieferantenwechsel, Dringlichkeit.Mittel,
                           "Kunde will wechseln, keine Rechnungsfrage.", "m.schmidt");

        Assert.That(ticket.EinordnungVerlauf, Has.Count.EqualTo(2));
        Assert.That(ticket.EinordnungVerlauf[0].Urheber, Is.EqualTo(Urheber.KI));
        Assert.That(ticket.EinordnungVerlauf[0].Kategorie, Is.EqualTo(Kategorie.Rechnung));
        Assert.That(ticket.AktuelleEinordnung!.Kategorie, Is.EqualTo(Kategorie.Lieferantenwechsel));
        Assert.That(ticket.AktuelleEinordnung.Bearbeiter, Is.EqualTo("m.schmidt"));
        Assert.That(ticket.Team, Is.EqualTo(Team.Vertrieb));
    }

    [Test]
    public void Eine_Korrektur_fuehrt_zurueck_auf_WartetAufFreigabe()
    {
        var ticket = NeuesTicket();
        ticket.Einordnen(Vorschlag(konfidenz: 0.4), KiModell);
        Assert.That(ticket.Status, Is.EqualTo(TicketStatus.ManuellePruefung));

        ticket.Korrigieren(Kategorie.Umzug, Dringlichkeit.Mittel, "Kunde zieht um.", "m.schmidt");

        Assert.That(ticket.Status, Is.EqualTo(TicketStatus.WartetAufFreigabe));
    }

    [Test]
    public void Vor_der_Einordnung_gibt_es_nichts_zu_korrigieren()
    {
        var ticket = NeuesTicket();

        Assert.That(() => ticket.Korrigieren(Kategorie.Umzug, Dringlichkeit.Mittel, "Grund", "m.schmidt"),
                    Throws.InvalidOperationException);
    }

    [Test]
    public void Ein_freigegebenes_Ticket_kann_nicht_mehr_korrigiert_werden()
    {
        var ticket = NeuesTicket();
        ticket.Einordnen(Vorschlag(), KiModell);
        ticket.Freigeben();

        Assert.That(() => ticket.Korrigieren(Kategorie.Umzug, Dringlichkeit.Mittel, "Grund", "m.schmidt"),
                    Throws.InvalidOperationException);
    }

    [Test]
    public void Auch_ein_Mensch_kann_die_Dringlichkeit_eines_Notfalls_nicht_senken()
    {
        var ticket = NeuesTicket("Im Treppenhaus ist starker Gasgeruch!");
        ticket.Einordnen(Vorschlag(Kategorie.Stoerung, Dringlichkeit.Hoch), KiModell);
        ticket.Korrigieren(Kategorie.Sonstiges, Dringlichkeit.Niedrig, "Wirkt harmlos.", "m.schmidt");

        Assert.That(ticket.AktuelleEinordnung!.Dringlichkeit, Is.EqualTo(Dringlichkeit.Hoch));
    }
}
