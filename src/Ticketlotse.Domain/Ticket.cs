namespace Ticketlotse.Domain;

public sealed class Ticket
{
    // Fachregel: Bei diesen Begriffen ist es immer dringend – egal, was die KI sagt.
    private static readonly string[] Notfallbegriffe =
        ["gasgeruch", "stromausfall", "kein strom", "kein wasser", "wasserrohrbruch"];

    public Guid Id { get; } = Guid.NewGuid();
    public string Absender { get; }
    public string Betreff { get; }
    public string Text { get; }
    public DateTimeOffset EingegangenAm { get; }
    public TicketStatus Status { get; private set; } = TicketStatus.Neu;
    public Klassifizierung? Klassifizierung { get; private set; }

    public Team? Team => Klassifizierung is null ? null : Zustaendigkeit.Fuer(Klassifizierung.Kategorie);

    private Ticket(string absender, string betreff, string text, DateTimeOffset eingegangenAm)
    {
        Absender = absender;
        Betreff = betreff;
        Text = text;
        EingegangenAm = eingegangenAm;
    }

    // Der Betreff ist freiwillig – E-Mails ohne Betreff kommen vor.
    public static Ticket Erstellen(string absender, string? betreff, string text, DateTimeOffset? eingegangenAm = null)
    {
        if (string.IsNullOrWhiteSpace(absender))
            throw new ArgumentException("Der Absender fehlt.", nameof(absender));
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Der Text der Anfrage fehlt.", nameof(text));

        return new Ticket(absender.Trim(), betreff?.Trim() ?? "", text.Trim(), eingegangenAm ?? DateTimeOffset.UtcNow);
    }

    public bool EnthaeltNotfallhinweis =>
        Notfallbegriffe.Any(begriff => $"{Betreff} {Text}".Contains(begriff, StringComparison.OrdinalIgnoreCase));

    public void Einordnen(Klassifizierung vorschlag)
    {
        ArgumentNullException.ThrowIfNull(vorschlag);
        if (Status != TicketStatus.Neu)
            throw new InvalidOperationException("Das Ticket wurde bereits eingeordnet.");

        Klassifizierung = EnthaeltNotfallhinweis
            ? vorschlag with { Dringlichkeit = Dringlichkeit.Hoch }
            : vorschlag;

        Status = vorschlag.Konfidenz.IstAusreichend
            ? TicketStatus.WartetAufFreigabe
            : TicketStatus.ManuellePruefung;
    }

    public void Freigeben()
    {
        if (Status is not (TicketStatus.WartetAufFreigabe or TicketStatus.ManuellePruefung))
            throw new InvalidOperationException("Nur eingeordnete Tickets können freigegeben werden.");

        Status = TicketStatus.Freigegeben;
    }
}