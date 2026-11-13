using System.Text.Json;
using Microsoft.Extensions.Logging;
using Ticketlotse.Domain;

namespace Ticketlotse.Application;

/// <summary>
/// Die Anwendungsfälle rund um ein Ticket. Enthält selbst keine Fachregeln –
/// die stehen im Domain-Projekt – sondern orchestriert nur Domain und Ablage.
/// </summary>
public sealed class Ticketverwaltung(
    ITicketRepository repository,
    ITicketKlassifizierer klassifizierer,
    ILogger<Ticketverwaltung> protokoll)
{
    public async Task<Ticket> ErfassenAsync(
    string absender, string? betreff, string text, CancellationToken abbruch = default)
    {
        var ticket = Ticket.Erstellen(absender, betreff, text);

        // Erst sichern, dann einordnen: Die Anfrage darf nicht verlorengehen,
        // nur weil das Modell gerade nicht antwortet.
        await repository.HinzufuegenAsync(ticket, abbruch);
        await VersucheEinzuordnenAsync(ticket, abbruch);

        return ticket;
    }

    private async Task VersucheEinzuordnenAsync(Ticket ticket, CancellationToken abbruch)
    {
        try
        {
            var vorschlag = await klassifizierer.EinordnenAsync(ticket, abbruch);
            ticket.Einordnen(vorschlag.Klassifizierung, vorschlag.Modell);
            await repository.AktualisierenAsync(ticket, abbruch);

            protokoll.LogInformation("Ticket {TicketId} von {Modell} als {Kategorie} eingeordnet",
                ticket.Id, vorschlag.Modell, vorschlag.Klassifizierung.Kategorie);
        }
        catch (KlassifizierungNichtMoeglich fehler)
        {
            // Das Ticket bleibt auf "Neu" liegen und kann später erneut eingeordnet werden.
            protokoll.LogWarning("Ticket {TicketId} konnte nicht eingeordnet werden: {Grund}",
                ticket.Id, fehler.Message);
        }
    }

    public async Task<Ticket?> ErneutEinordnenAsync(Guid id, CancellationToken abbruch = default)
    {
        var ticket = await repository.FindenAsync(id, abbruch);
        if (ticket is null)
            return null;

        await VersucheEinzuordnenAsync(ticket, abbruch);

        return ticket;
    }

    public async Task<Ticket?> KorrigierenAsync(Guid id, Kategorie kategorie, Dringlichkeit dringlichkeit, string begruendung, string bearbeiter, CancellationToken abbruch = default)

    {
        var ticket = await repository.FindenAsync(id, abbruch);
        if (ticket is null)
            return null;
        var options = new JsonSerializerOptions { WriteIndented = true };
        var vorher = ticket.AktuelleEinordnung;
        ticket.Korrigieren(kategorie, dringlichkeit, begruendung, bearbeiter);
        await repository.AktualisierenAsync(ticket, abbruch);

        protokoll.LogInformation(
            "Ticket {TicketId} von {Bearbeiter} korrigiert: {AlteKategorie}/{AlteDringlichkeit} → {NeueKategorie}/{NeueDringlichkeit}",
            ticket.Id, bearbeiter,
            vorher?.Kategorie, vorher?.Dringlichkeit,
            ticket.AktuelleEinordnung!.Kategorie, ticket.AktuelleEinordnung.Dringlichkeit);

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