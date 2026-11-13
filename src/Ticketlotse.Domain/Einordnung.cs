namespace Ticketlotse.Domain;

/// <summary>
/// Ein Eintrag im Einordnungsverlauf eines Tickets: entweder der Vorschlag der KI
/// oder die Korrektur durch einen Menschen. Beide Formen entstehen ausschließlich
/// über die Erzeugungsmethoden, damit keine Mischform möglich ist.
/// </summary>
public sealed record Einordnung
{
    public Kategorie Kategorie { get; }
    public Dringlichkeit Dringlichkeit { get; }
    public string Begruendung { get; }
    public Urheber Urheber { get; }

    /// <summary>Das Modell bei der KI, der Anmeldename bei einem Menschen.</summary>
    public string Bearbeiter { get; }

    public DateTimeOffset ErfolgtAm { get; }

    /// <summary>Nur die KI liefert eine Konfidenz; sie entscheidet über den Status des Tickets.</summary>
    public Konfidenz? Konfidenz { get; }

    /// <summary>Den Antworttext entwirft nur die KI – nach einer Korrektur formuliert der Mensch selbst.</summary>
    public string? Antwortvorschlag { get; }

    private Einordnung(
        Kategorie kategorie,
        Dringlichkeit dringlichkeit,
        string begruendung,
        Urheber urheber,
        string bearbeiter,
        DateTimeOffset erfolgtAm,
        Konfidenz? konfidenz,
        string? antwortvorschlag)
    {
        Kategorie = kategorie;
        Dringlichkeit = dringlichkeit;
        Begruendung = begruendung;
        Urheber = urheber;
        Bearbeiter = bearbeiter;
        ErfolgtAm = erfolgtAm;
        Konfidenz = konfidenz;
        Antwortvorschlag = antwortvorschlag;
    }

    /// <param name="modell">Das Modell, das geantwortet hat – es wird als Bearbeiter geführt.</param>
    public static Einordnung VonKi(Klassifizierung vorschlag, string modell, DateTimeOffset? erfolgtAm = null)
    {
        ArgumentNullException.ThrowIfNull(vorschlag);
        if (string.IsNullOrWhiteSpace(modell))
            throw new ArgumentException("Das verwendete Modell fehlt.", nameof(modell));

        return new Einordnung(
            vorschlag.Kategorie,
            vorschlag.Dringlichkeit,
            vorschlag.Begruendung,
            Urheber.KI,
            modell.Trim(),
            erfolgtAm ?? DateTimeOffset.UtcNow,
            vorschlag.Konfidenz,
            vorschlag.Antwortvorschlag);
    }

    public static Einordnung VonMensch(
        Kategorie kategorie,
        Dringlichkeit dringlichkeit,
        string begruendung,
        string bearbeiter,
        DateTimeOffset? erfolgtAm = null)
    {
        if (string.IsNullOrWhiteSpace(begruendung))
            throw new ArgumentException("Eine Korrektur muss begründet werden.", nameof(begruendung));
        if (string.IsNullOrWhiteSpace(bearbeiter))
            throw new ArgumentException("Der Bearbeiter fehlt.", nameof(bearbeiter));

        return new Einordnung(
            kategorie,
            dringlichkeit,
            begruendung.Trim(),
            Urheber.Mensch,
            bearbeiter.Trim(),
            erfolgtAm ?? DateTimeOffset.UtcNow,
            konfidenz: null,
            antwortvorschlag: null);
    }
}