using Ticketlotse.Domain;

namespace Ticketlotse.Application;

/// <summary>
/// Zugriff auf die gespeicherten Tickets. Die Anwendung kennt nur dieses Interface,
/// die konkrete Ablage (Arbeitsspeicher, später Datenbank) liegt in der Infrastruktur.
/// </summary>
public interface ITicketRepository
{
    Task HinzufuegenAsync(Ticket ticket, CancellationToken abbruch = default);

    Task<Ticket?> FindenAsync(Guid id, CancellationToken abbruch = default);

    /// <param name="status">Ohne Angabe werden alle Tickets geliefert.</param>
    Task<IReadOnlyList<Ticket>> SuchenAsync(TicketStatus? status = null, CancellationToken abbruch = default);

    Task AktualisierenAsync(Ticket ticket, CancellationToken abbruch = default);
}
