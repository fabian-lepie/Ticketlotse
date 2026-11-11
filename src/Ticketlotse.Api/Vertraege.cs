using Ticketlotse.Domain;

namespace Ticketlotse.Api;

/// <summary>Was der Aufrufer beim Anlegen eines Tickets schickt.</summary>
public sealed record TicketAnlegenAnfrage(string Absender, string? Betreff, string Text);

/// <summary>
/// Das Ticket nach außen. Enums werden als Text ausgegeben, damit die Antwort
/// auch ohne Kenntnis unserer Reihenfolge lesbar bleibt.
/// Die Felder der Einordnung sind leer, solange die KI das Ticket nicht bewertet hat.
/// </summary>
public sealed record TicketAntwort(
    Guid Id,
    string Absender,
    string Betreff,
    string Text,
    DateTimeOffset EingegangenAm,
    string Status,
    string? Kategorie,
    string? Dringlichkeit,
    string? Team,
    double? Sicherheit,
    string? Begruendung,
    string? Antwortvorschlag)
{
    public static TicketAntwort Von(Ticket ticket) => new(
        ticket.Id,
        ticket.Absender,
        ticket.Betreff,
        ticket.Text,
        ticket.EingegangenAm,
        ticket.Status.ToString(),
        ticket.Klassifizierung?.Kategorie.ToString(),
        ticket.Klassifizierung?.Dringlichkeit.ToString(),
        ticket.Team?.ToString(),
        ticket.Klassifizierung?.Sicherheit.Wert,
        ticket.Klassifizierung?.Begruendung,
        ticket.Klassifizierung?.Antwortvorschlag);
}
