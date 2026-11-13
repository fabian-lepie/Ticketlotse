// src/Ticketlotse.Application/ITicketKlassifizierer.cs
using Ticketlotse.Domain;

namespace Ticketlotse.Application;

/// <summary>Was die KI vorschlägt, und welches Modell geantwortet hat.</summary>
public sealed record KiVorschlag(Klassifizierung Klassifizierung, string Modell);

/// <summary>
/// Lässt ein Ticket von einer KI einordnen. Die Umsetzung liegt in der Infrastruktur;
/// von Prompts, HTTP und JSON weiß diese Schicht nichts.
/// </summary>
public interface ITicketKlassifizierer
{
    Task<KiVorschlag> EinordnenAsync(Ticket ticket, CancellationToken abbruch = default);
}

/// <summary>Die Einordnung ist gescheitert – Zeitüberschreitung, unbrauchbare Antwort, kein Kontingent.</summary>
public sealed class KlassifizierungNichtMoeglich(string nachricht, Exception? ursache = null)
    : Exception(nachricht, ursache);