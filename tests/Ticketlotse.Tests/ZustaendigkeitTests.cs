using NUnit.Framework;
using Ticketlotse.Domain;

namespace Ticketlotse.Tests;

public class ZustaendigkeitTests
{
    [TestCase(Kategorie.Zaehlerstand, Team.Messwesen)]
    [TestCase(Kategorie.Abschlag, Team.Abrechnung)]
    [TestCase(Kategorie.Rechnung, Team.Abrechnung)]
    [TestCase(Kategorie.Umzug, Team.Kundenservice)]
    [TestCase(Kategorie.Lieferantenwechsel, Team.Vertrieb)]
    [TestCase(Kategorie.TarifVertrag, Team.Vertrieb)]
    [TestCase(Kategorie.Stoerung, Team.Netzbetrieb)]
    [TestCase(Kategorie.Sonstiges, Team.Kundenservice)]
    public void Jede_Kategorie_hat_ein_zustaendiges_Team(Kategorie kategorie, Team erwartet)
    {
        Assert.That(Zustaendigkeit.Fuer(kategorie), Is.EqualTo(erwartet));
    }
}