namespace Ticketlotse.Domain;

public sealed record Konfidenz
{
    public const double Schwellenwert = 0.70;

    public double Wert { get; }

    public Konfidenz(double wert)
    {
        if (wert is < 0.0 or > 1.0)
            throw new ArgumentOutOfRangeException(nameof(wert), "Die Konfidenz muss zwischen 0 und 1 liegen.");
        Wert = wert;
    }

    public bool IstAusreichend => Wert >= Schwellenwert;

    public override string ToString() => $"{Wert:P0}";
}
