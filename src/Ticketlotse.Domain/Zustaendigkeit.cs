namespace Ticketlotse.Domain;

public static class Zustaendigkeit
{
    public static Team Fuer(Kategorie kategorie) => kategorie switch
    {
        Kategorie.Zaehlerstand => Team.Messwesen,
        Kategorie.Abschlag or Kategorie.Rechnung => Team.Abrechnung,
        Kategorie.Umzug or Kategorie.Sonstiges => Team.Kundenservice,
        Kategorie.Lieferantenwechsel or Kategorie.TarifVertrag => Team.Vertrieb,
        Kategorie.Stoerung => Team.Netzbetrieb,
        _ => throw new ArgumentOutOfRangeException(nameof(kategorie))
    };
}