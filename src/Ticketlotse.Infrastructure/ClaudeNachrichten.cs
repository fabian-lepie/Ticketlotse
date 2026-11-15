using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Ticketlotse.Infrastructure;

// Das Format der Messages-API. Alle Antwortfelder sind optional –
// einer fremden Schnittstelle glauben wir nichts.

internal sealed record ClaudeAnfrage(
    string Model,
    int MaxTokens,
    [property: JsonPropertyName("system")] string Systemprompt,
    IReadOnlyList<ClaudeNachricht> Messages,
    ClaudeAusgabeKonfiguration OutputConfig);

internal sealed record ClaudeNachricht(string Role, string Content);

internal sealed record ClaudeAusgabeKonfiguration(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Effort,
    ClaudeFormat Format);

internal sealed record ClaudeFormat(string Type, JsonNode Schema);

internal sealed record ClaudeAntwort(
    string? Model,
    string? StopReason,
    IReadOnlyList<ClaudeInhaltsblock>? Content);

internal sealed record ClaudeInhaltsblock(string? Type, string? Text);

/// <summary>Was im Textblock steht – unser JSON-Schema.</summary>
internal sealed record ClaudeEinordnung(
    string? Kategorie,
    string? Dringlichkeit,
    double? Konfidenz,
    string? Begruendung,
    string? Antwortvorschlag);