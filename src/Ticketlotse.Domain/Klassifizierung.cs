namespace Ticketlotse.Domain;

public sealed record Klassifizierung(
    Kategorie Kategorie,
    Dringlichkeit Dringlichkeit,
    Sicherheit Sicherheit,
    string Begruendung,
    string Antwortvorschlag);