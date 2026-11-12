using Microsoft.AspNetCore.Http.HttpResults;
using Ticketlotse.Application;
using Ticketlotse.Domain;

namespace Ticketlotse.Api;

public static class TicketEndpunkte
{
    public static void MapTicketEndpunkte(this WebApplication app)
    {
        var tickets = app.MapGroup("/tickets");

        tickets.MapPost("", Anlegen);
        tickets.MapGet("", Suchen);
        tickets.MapGet("/{id:guid}", Abrufen);
        tickets.MapPut("/{id:guid}/freigabe", Freigeben);
    }

    private static async Task<Results<Created<TicketAntwort>, ProblemHttpResult>> Anlegen(
        TicketAnlegenAnfrage anfrage,
        Ticketverwaltung verwaltung,
        ILoggerFactory loggerFabrik,
        CancellationToken abbruch)
    {
        var protokoll = loggerFabrik.CreateLogger(typeof(TicketEndpunkte));

        Ticket ticket;
        try
        {
            ticket = await verwaltung.ErfassenAsync(anfrage.Absender, anfrage.Betreff, anfrage.Text, abbruch);
        }
        catch (ArgumentException fehler)
        {
            // Die Pflichtangaben prüft das Ticket selbst; für den Aufrufer ist das ein 400.
            protokoll.LogWarning("Ticket abgelehnt: {Grund}", fehler.Message);
            return TypedResults.Problem(fehler.Message, statusCode: StatusCodes.Status400BadRequest);
        }

        protokoll.LogInformation("Ticket {TicketId} erfasst", ticket.Id);
        return TypedResults.Created($"/tickets/{ticket.Id}", TicketAntwort.Von(ticket));
    }

    private static async Task<Results<Ok<IEnumerable<TicketAntwort>>, ProblemHttpResult>> Suchen(
        string? status,
        Ticketverwaltung verwaltung,
        CancellationToken abbruch)
    {
        TicketStatus? filter = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!IstBekannterStatus(status, out var erkannt))
            {
                return TypedResults.Problem(
                    $"Unbekannter Status \"{status}\". Möglich sind: {string.Join(", ", Enum.GetNames<TicketStatus>())}.",
                    statusCode: StatusCodes.Status400BadRequest);
            }
            filter = erkannt;
        }

        var treffer = await verwaltung.SuchenAsync(filter, abbruch);
        return TypedResults.Ok(treffer.Select(TicketAntwort.Von));
    }

    private static bool IstBekannterStatus(string statusZuUeberpruefen, out TicketStatus status)
    {
        var statusName = Enum.GetNames<TicketStatus>()
            .FirstOrDefault(n => string.Equals(n, statusZuUeberpruefen, StringComparison.OrdinalIgnoreCase));
        if (statusName == null)
        {
            status = default;
            return false;
        }
        status = Enum.Parse<TicketStatus>(statusName);
        return true;
    }

    private static async Task<Results<Ok<TicketAntwort>, NotFound>> Abrufen(
        Guid id, 
        Ticketverwaltung verwaltung, 
        CancellationToken abbruch)
    {
        var ticket = await verwaltung.FindenAsync(id, abbruch);
        return ticket is null ? TypedResults.NotFound() : TypedResults.Ok(TicketAntwort.Von(ticket));
    }

    private static async Task<Results<Ok<TicketAntwort>, NotFound, ProblemHttpResult>> Freigeben(
        Guid id,
        Ticketverwaltung verwaltung,
        ILoggerFactory loggerFabrik,
        CancellationToken abbruch)
    {
        var protokoll = loggerFabrik.CreateLogger(typeof(TicketEndpunkte));

        try
        {
            var ticket = await verwaltung.FreigebenAsync(id, abbruch);
            if (ticket is null)
            {
                protokoll.LogInformation("Freigabe für unbekanntes Ticket {TicketId} angefragt", id);
                return TypedResults.NotFound();
            }

            protokoll.LogInformation("Ticket {TicketId} wurde freigegeben", ticket.Id);
            return TypedResults.Ok(TicketAntwort.Von(ticket));
        }
        catch (InvalidOperationException fehler)
        {
            // Ticket ist noch nicht eingeordnet: fachlicher Konflikt, kein Serverfehler.
            protokoll.LogWarning("Freigabe für Ticket {TicketId} abgelehnt: {Grund}", id, fehler.Message);
            return TypedResults.Problem(fehler.Message, statusCode: StatusCodes.Status409Conflict);
        }
    }
}