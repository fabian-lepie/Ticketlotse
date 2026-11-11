namespace Ticketlotse.Domain;

public enum Kategorie
{
    Zaehlerstand, Abschlag, Rechnung, Umzug, Lieferantenwechsel, TarifVertrag, Stoerung, Sonstiges
}

public enum Dringlichkeit { Niedrig, Mittel, Hoch }

public enum Team { Messwesen, Abrechnung, Kundenservice, Vertrieb, Netzbetrieb }

public enum TicketStatus { Neu, WartetAufFreigabe, ManuellePruefung, Freigegeben }