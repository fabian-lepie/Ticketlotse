namespace Ticketlotse.Infrastructure;

public sealed class ClaudeEinstellungen
{
    public string Modell { get; set; } = "claude-haiku-4-5";
    public int MaxTokens { get; set; } = 4096;
    /// <summary>Nur für Modelle, die den Aufwand-Parameter unterstützen (Opus). Leer lassen für Haiku und Sonnet.</summary>
    public string? Aufwand { get; set; }

    public int ZeitueberschreitungSekunden { get; set; } = 30;
}