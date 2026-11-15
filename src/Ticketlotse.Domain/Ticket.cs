using System.Text.RegularExpressions;

namespace Ticketlotse.Domain;

public sealed partial class Ticket
{
    // Fachregel: Diese Muster gelten als Notfall. Bewusst als Muster und nicht als
    // Wortliste – "kein Strom", "keinen Strom" und "ohne Strom" meinen dasselbe.
    [GeneratedRegex(
    """
        gasgeruch
        | nach\s+gas\b
        | stromausfall
        | wasserrohrbruch
        | kein(?:e|en)?\s+(?:strom|wasser|gas)\b
        | ohne\s+(?:strom|wasser|gas)\b
        """,
    RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace)]
    private static partial Regex Notfallmuster();

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

    public bool EnthaeltNotfallhinweis => Notfallmuster().IsMatch($"{Betreff} {Text}");

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

    public void Einordnen(Klassifizierung vorschlag, string modell)
    {
        ArgumentNullException.ThrowIfNull(vorschlag);
        if (Status != TicketStatus.Neu)
            throw new InvalidOperationException("Das Ticket wurde bereits eingeordnet.");

        _einordnungVerlauf.Add(Einordnung.VonKi(
            vorschlag with
            {
                Kategorie = MitNotfallregel(vorschlag.Kategorie),
                Dringlichkeit = MitNotfallregel(vorschlag.Dringlichkeit),
            },
            modell));

        Status = vorschlag.Konfidenz.IstAusreichend
            ? TicketStatus.WartetAufFreigabe
            : TicketStatus.ManuellePruefung;
    }

    public void Korrigieren(Kategorie kategorie, Dringlichkeit dringlichkeit, string begruendung, string bearbeiter)
    {
        if (!IstEingeordnet)
            throw new InvalidOperationException("Nur eingeordnete Tickets können korrigiert werden.");

        // Die Kategorie läuft hier absichtlich nicht durch die Notfallregel: Sonst könnte
        // ein falsch erkannter Notfall das zuständige Team nie wieder verlassen.
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

    // Sicherheitsregel, Teil 1: Notfallmuster erzwingen hohe Dringlichkeit –
    // gegen die KI und gegen den Menschen. Hier gibt es keine Ausnahme.
    private Dringlichkeit MitNotfallregel(Dringlichkeit vorgeschlagen) =>
        EnthaeltNotfallhinweis ? Dringlichkeit.Hoch : vorgeschlagen;

    // Sicherheitsregel, Teil 2: Die KI darf einen Notfall nicht wegkategorisieren und
    // damit am Netzbetrieb vorbeileiten. Für einen Menschen gilt das nicht – er trägt
    // die Verantwortung, sein Name steht im Verlauf, und er muss weiterleiten können.
    private Kategorie MitNotfallregel(Kategorie vorgeschlagen) =>
        EnthaeltNotfallhinweis ? Kategorie.Stoerung : vorgeschlagen;
}