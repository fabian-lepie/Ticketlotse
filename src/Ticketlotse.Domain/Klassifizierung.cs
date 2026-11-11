namespace Ticketlotse.Domain;

public sealed record Klassifizierung(
    Kategorie Kategorie,
    Dringlichkeit Dringlichkeit,
    Konfidenz Konfidenz,
    string Begruendung,
    string Antwortvorschlag);
