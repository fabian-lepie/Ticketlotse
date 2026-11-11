using Ticketlotse.Domain;

namespace Ticketlotse.Application;

/// <summary>
/// Die Anwendungsfälle rund um ein Ticket. Enthält selbst keine Fachregeln –
/// die stehen im Domain-Projekt – sondern orchestriert nur Domain und Ablage.
/// </summary>
public sealed class Ticketverwaltung(ITicketRepository repository)
{
    public async Task<Ticket> ErfassenAsync(
        string absender,
        string? betreff,
        string text,
        CancellationToken abbruch = default)
    {
        var ticket = Ticket.Erstellen(absender, betreff, text);
        await repository.HinzufuegenAsync(ticket, abbruch);
        return ticket;
    }

    public Task<Ticket?> FindenAsync(Guid id, CancellationToken abbruch = default) =>
        repository.FindenAsync(id, abbruch);

    public Task<IReadOnlyList<Ticket>> SuchenAsync(
        TicketStatus? status = null,
        CancellationToken abbruch = default) =>
        repository.SuchenAsync(status, abbruch);

    /// <summary>
    /// Gibt den KI-Vorschlag frei. Liefert <c>null</c>, wenn es das Ticket nicht gibt;
    /// ob eine Freigabe fachlich erlaubt ist, entscheidet das Ticket selbst.
    /// </summary>
    public async Task<Ticket?> FreigebenAsync(Guid id, CancellationToken abbruch = default)
    {
        var ticket = await repository.FindenAsync(id, abbruch);
        if (ticket is null)
            return null;

        ticket.Freigeben();
        await repository.AktualisierenAsync(ticket, abbruch);
        return ticket;
    }
}
