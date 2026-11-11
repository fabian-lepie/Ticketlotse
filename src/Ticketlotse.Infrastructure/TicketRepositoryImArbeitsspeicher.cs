using System.Collections.Concurrent;
using Ticketlotse.Application;
using Ticketlotse.Domain;

namespace Ticketlotse.Infrastructure;

/// <summary>
/// Ablage im Arbeitsspeicher – genug für Demo und Tests.
/// In Etappe 4 tritt an diese Stelle ein EF-Core-Repository mit MS SQL.
/// </summary>
public sealed class TicketRepositoryImArbeitsspeicher : ITicketRepository
{
    private readonly ConcurrentDictionary<Guid, Ticket> _tickets = new();

    public Task HinzufuegenAsync(Ticket ticket, CancellationToken abbruch = default)
    {
        ArgumentNullException.ThrowIfNull(ticket);
        _tickets[ticket.Id] = ticket;
        return Task.CompletedTask;
    }

    public Task<Ticket?> FindenAsync(Guid id, CancellationToken abbruch = default) =>
        Task.FromResult(_tickets.TryGetValue(id, out var ticket) ? ticket : null);

    public Task<IReadOnlyList<Ticket>> SuchenAsync(
        TicketStatus? status = null,
        CancellationToken abbruch = default)
    {
        IReadOnlyList<Ticket> treffer = _tickets.Values
            .Where(ticket => status is null || ticket.Status == status)
            .OrderBy(ticket => ticket.EingegangenAm)
            .ToList();

        return Task.FromResult(treffer);
    }

    // Die Tickets liegen als Objekt im Speicher, Änderungen sind damit schon sichtbar.
    // Die Methode bleibt trotzdem, damit die Aufrufer für die Datenbank richtig gebaut sind.
    public Task AktualisierenAsync(Ticket ticket, CancellationToken abbruch = default) =>
        Task.CompletedTask;
}
