namespace Ticketlotse.Domain;

public sealed class Ticket
{
    // Fachregel: Bei diesen Begriffen ist es immer dringend – egal, wer etwas anderes vorschlägt.
    private static readonly string[] Notfallbegriffe =
        ["gasgeruch", "stromausfall", "kein strom", "kein wasser", "wasserrohrbruch"];

    private readonly List<Einordnung> _einordnungVerlauf = [];

    public Guid Id { get; } = Guid.NewGuid();
    public string Absender { get; }
    public string Betreff { get; }
    public string Text { get; }
    public DateTimeOffset EingegangenAm { get; }
    public TicketStatus Status { get; private set; } = TicketStatus.Neu;

    /// <summary>Der KI-Vorschlag und alle menschlichen Korrekturen in zeitlicher Reihenfolge.</summary>
    public IReadOnlyList<Einordnung> EinordnungVerlauf => _einordnungVerlauf;

    /// <summary>Die zuletzt gültige Einordnung; sie bestimmt Kategorie, Dringlichkeit und Team.</summary>
    public Einordnung? AktuelleEinordnung => _einordnungVerlauf.Count == 0 ? null : _einordnungVerlauf[^1];

    public Team? Team => AktuelleEinordnung is null ? null : Zustaendigkeit.Fuer(AktuelleEinordnung.Kategorie);

    private bool IstEingeordnet =>
        Status is TicketStatus.WartetAufFreigabe or TicketStatus.ManuellePruefung;

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

    public void Einordnen(Klassifizierung vorschlag, string modell)
    {
        ArgumentNullException.ThrowIfNull(vorschlag);
        if (Status != TicketStatus.Neu)
            throw new InvalidOperationException("Das Ticket wurde bereits eingeordnet.");

        _einordnungVerlauf.Add(Einordnung.VonKi(
            vorschlag with { Dringlichkeit = MitNotfallregel(vorschlag.Dringlichkeit) },
            modell));

        Status = vorschlag.Konfidenz.IstAusreichend
            ? TicketStatus.WartetAufFreigabe
            : TicketStatus.ManuellePruefung;
    }

    public void Korrigieren(Kategorie kategorie, Dringlichkeit dringlichkeit, string begruendung, string bearbeiter)
    {
        if (!IstEingeordnet)
            throw new InvalidOperationException("Nur eingeordnete Tickets können korrigiert werden.");

        _einordnungVerlauf.Add(Einordnung.VonMensch(kategorie, MitNotfallregel(dringlichkeit), begruendung, bearbeiter));

        // Durch die neue Kategorie ist oft ein anderes Team zuständig – dort wird erneut geprüft.
        Status = TicketStatus.WartetAufFreigabe;
    }

    public void Freigeben()
    {
        if (!IstEingeordnet)
            throw new InvalidOperationException("Nur eingeordnete Tickets können freigegeben werden.");

        Status = TicketStatus.Freigegeben;
    }

    // Sicherheitsregel: Notfallbegriffe erzwingen hohe Dringlichkeit, unabhängig vom Urheber.
    private Dringlichkeit MitNotfallregel(Dringlichkeit vorgeschlagen) =>
        EnthaeltNotfallhinweis ? Dringlichkeit.Hoch : vorgeschlagen;
}