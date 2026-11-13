using NUnit.Framework;
using Ticketlotse.Domain;

namespace Ticketlotse.Tests;

public class EinordnungTests
{
    private static readonly DateTimeOffset Zeitpunkt = new(2026, 11, 12, 9, 30, 0, TimeSpan.Zero);

    private static Klassifizierung Vorschlag(
        Kategorie kategorie = Kategorie.Abschlag,
        Dringlichkeit dringlichkeit = Dringlichkeit.Mittel,
        double konfidenz = 0.9) =>
        new(kategorie, dringlichkeit, new Konfidenz(konfidenz), "Begründung der KI", "Antwortvorschlag der KI");

    [Test]
    public void VonKi_nennt_das_Modell_als_Bearbeiter()
    {
        var einordnung = Einordnung.VonKi(
            Vorschlag(Kategorie.Stoerung, Dringlichkeit.Hoch), "claude-sonnet-5", Zeitpunkt);

        Assert.That(einordnung.Urheber, Is.EqualTo(Urheber.KI));
        Assert.That(einordnung.Bearbeiter, Is.EqualTo("claude-sonnet-5"));
        Assert.That(einordnung.Kategorie, Is.EqualTo(Kategorie.Stoerung));
        Assert.That(einordnung.Dringlichkeit, Is.EqualTo(Dringlichkeit.Hoch));
        Assert.That(einordnung.ErfolgtAm, Is.EqualTo(Zeitpunkt));
    }

    [Test]
    public void VonKi_behaelt_Konfidenz_Begruendung_und_Antwortvorschlag()
    {
        var einordnung = Einordnung.VonKi(Vorschlag(konfidenz: 0.42), "claude-sonnet-5", Zeitpunkt);

        Assert.That(einordnung.Konfidenz!.Wert, Is.EqualTo(0.42));
        Assert.That(einordnung.Begruendung, Is.EqualTo("Begründung der KI"));
        Assert.That(einordnung.Antwortvorschlag, Is.EqualTo("Antwortvorschlag der KI"));
    }

    [Test]
    public void VonMensch_hat_keine_Konfidenz()
    {
        var einordnung = Einordnung.VonMensch(
            Kategorie.Umzug, Dringlichkeit.Niedrig, "Kunde zieht um, keine Abschlagsfrage.", "m.schmidt", Zeitpunkt);

        Assert.That(einordnung.Urheber, Is.EqualTo(Urheber.Mensch));
        Assert.That(einordnung.Bearbeiter, Is.EqualTo("m.schmidt"));
        Assert.That(einordnung.Konfidenz, Is.Null);
        Assert.That(einordnung.Kategorie, Is.EqualTo(Kategorie.Umzug));
        Assert.That(einordnung.Begruendung, Is.EqualTo("Kunde zieht um, keine Abschlagsfrage."));
    }

    [TestCase("")]
    [TestCase("   ")]
    public void Eine_KI_Einordnung_ohne_Modell_ist_nicht_erlaubt(string modell)
    {
        Assert.That(() => Einordnung.VonKi(Vorschlag(), modell, Zeitpunkt),
                    Throws.InstanceOf<ArgumentException>());
    }

    [TestCase("")]
    [TestCase("   ")]
    public void Eine_menschliche_Einordnung_ohne_Bearbeiter_ist_nicht_erlaubt(string bearbeiter)
    {
        Assert.That(() => Einordnung.VonMensch(
                        Kategorie.Umzug, Dringlichkeit.Niedrig, "Grund", bearbeiter, Zeitpunkt),
                    Throws.InstanceOf<ArgumentException>());
    }

    [TestCase("")]
    [TestCase("   ")]
    public void Eine_menschliche_Einordnung_braucht_eine_Begruendung(string begruendung)
    {
        Assert.That(() => Einordnung.VonMensch(
                        Kategorie.Umzug, Dringlichkeit.Niedrig, begruendung, "m.schmidt", Zeitpunkt),
                    Throws.InstanceOf<ArgumentException>());
    }

    [Test]
    public void Ohne_Zeitangabe_gilt_der_aktuelle_Zeitpunkt()
    {
        var vorher = DateTimeOffset.UtcNow;

        var einordnung = Einordnung.VonMensch(
            Kategorie.Umzug, Dringlichkeit.Niedrig, "Grund", "m.schmidt");

        Assert.That(einordnung.ErfolgtAm, Is.InRange(vorher, DateTimeOffset.UtcNow));
    }
}