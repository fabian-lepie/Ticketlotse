// tests/Ticketlotse.Tests/FakeKlassifizierer.cs
using Ticketlotse.Application;
using Ticketlotse.Domain;

namespace Ticketlotse.Tests;

internal sealed class FakeKlassifizierer : ITicketKlassifizierer
{
    private readonly KiVorschlag? _vorschlag;
    private readonly int _scheiterndeAufrufe;

    public int Aufrufe { get; private set; }

    private FakeKlassifizierer(KiVorschlag? vorschlag, int scheiterndeAufrufe = 0)
    {
        _vorschlag = vorschlag;
        _scheiterndeAufrufe = scheiterndeAufrufe;
    }

    public static FakeKlassifizierer Liefert(
        Kategorie kategorie = Kategorie.Abschlag,
        Dringlichkeit dringlichkeit = Dringlichkeit.Mittel,
        double konfidenz = 0.9,
        string modell = "test-modell") => new(Vorschlag(kategorie, dringlichkeit, konfidenz, modell));

    public static FakeKlassifizierer Scheitert() => new(null);

    /// <summary>Erst eine Störung, danach antwortet das Modell – für die erneute Einordnung.</summary>
    public static FakeKlassifizierer ScheitertBeimErstenMal(string modell = "test-modell") =>
        new(Vorschlag(Kategorie.Abschlag, Dringlichkeit.Mittel, 0.9, modell), scheiterndeAufrufe: 1);

    private static KiVorschlag Vorschlag(
        Kategorie kategorie, Dringlichkeit dringlichkeit, double konfidenz, string modell) =>
        new(new Klassifizierung(kategorie, dringlichkeit, new Konfidenz(konfidenz),
                                "Begründung der KI", "Antwortvorschlag der KI"), modell);

    public Task<KiVorschlag> EinordnenAsync(Ticket ticket, CancellationToken abbruch = default)
    {
        Aufrufe++;

        if (_vorschlag is null || Aufrufe <= _scheiterndeAufrufe)
            throw new KlassifizierungNichtMoeglich("Das Modell antwortet nicht.");

        return Task.FromResult(_vorschlag);
    }
}