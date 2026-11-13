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
    double? Konfidenz,
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
        ticket.AktuelleEinordnung?.Kategorie.ToString(),
        ticket.AktuelleEinordnung?.Dringlichkeit.ToString(),
        ticket.Team?.ToString(),
        ticket.AktuelleEinordnung?.Konfidenz?.Wert,
        ticket.AktuelleEinordnung?.Begruendung,
        ticket.AktuelleEinordnung?.Antwortvorschlag);
}

public sealed record EinordnungAntwort(
  string Kategorie,
  string Dringlichkeit,
  string Begruendung,
  string Urheber,
  string Bearbeiter,
  DateTimeOffset ErfolgtAm,
  double? Konfidenz,
  string? Antwortvorschlag)
{
    public static EinordnungAntwort Von(Einordnung einordnung) => new(
        einordnung.Kategorie.ToString(),
        einordnung.Dringlichkeit.ToString(),
        einordnung.Begruendung,
        einordnung.Urheber.ToString(),
        einordnung.Bearbeiter,
        einordnung.ErfolgtAm,
        einordnung.Konfidenz?.Wert,
        einordnung.Antwortvorschlag);
}

/// <summary>Das Ticket mit seinem vollständigen Einordnungsverlauf – nur bei GET /tickets/{id}.</summary>
public sealed record TicketDetailAntwort(
    Guid Id,
    string Absender,
    string Betreff,
    string Text,
    DateTimeOffset EingegangenAm,
    string Status,
    string? Kategorie,
    string? Dringlichkeit,
    string? Team,
    double? Konfidenz,
    string? Begruendung,
    string? Antwortvorschlag,
    IReadOnlyList<EinordnungAntwort> Verlauf)
{
    public static TicketDetailAntwort Von(Ticket ticket)
    {
        var basis = TicketAntwort.Von(ticket);

        return new(
            basis.Id, basis.Absender, basis.Betreff, basis.Text, basis.EingegangenAm,
            basis.Status, basis.Kategorie, basis.Dringlichkeit, basis.Team,
            basis.Konfidenz, basis.Begruendung, basis.Antwortvorschlag,
            ticket.EinordnungVerlauf.Select(EinordnungAntwort.Von).ToList());
    }
}

/// <summary>Die menschliche Korrektur einer Einordnung.</summary>
public sealed record KorrekturAnfrage(
    string Kategorie,
    string Dringlichkeit,
    string Begruendung,
    string Bearbeiter);
