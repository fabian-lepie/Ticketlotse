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

    private static async Task<IResult> Anlegen(
        TicketAnlegenAnfrage anfrage,
        Ticketverwaltung verwaltung,
        CancellationToken abbruch)
    {
        Ticket ticket;
        try
        {
            ticket = await verwaltung.ErfassenAsync(anfrage.Absender, anfrage.Betreff, anfrage.Text, abbruch);
        }
        catch (ArgumentException fehler)
        {
            // Die Pflichtangaben prüft das Ticket selbst; für den Aufrufer ist das ein 400.
            return Results.Problem(fehler.Message, statusCode: StatusCodes.Status400BadRequest);
        }

        return Results.Created($"/tickets/{ticket.Id}", TicketAntwort.Von(ticket));
    }

    private static async Task<IResult> Suchen(
        string? status,
        Ticketverwaltung verwaltung,
        CancellationToken abbruch)
    {
        TicketStatus? filter = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!IstBekannterStatus(status, out var erkannt))
                return Results.Problem(
                    $"Unbekannter Status \"{status}\". Möglich sind: {string.Join(", ", Enum.GetNames<TicketStatus>())}.",
                    statusCode: StatusCodes.Status400BadRequest);

            filter = erkannt;
        }

        var treffer = await verwaltung.SuchenAsync(filter, abbruch);
        return Results.Ok(treffer.Select(TicketAntwort.Von));
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

    private static async Task<IResult> Abrufen(Guid id, Ticketverwaltung verwaltung, CancellationToken abbruch)
    {
        var ticket = await verwaltung.FindenAsync(id, abbruch);
        return ticket is null ? Results.NotFound() : Results.Ok(TicketAntwort.Von(ticket));
    }

    private static async Task<IResult> Freigeben(Guid id, Ticketverwaltung verwaltung, CancellationToken abbruch)
    {
        try
        {
            var ticket = await verwaltung.FreigebenAsync(id, abbruch);
            return ticket is null ? Results.NotFound() : Results.Ok(TicketAntwort.Von(ticket));
        }
        catch (InvalidOperationException fehler)
        {
            // Ticket ist noch nicht eingeordnet: fachlicher Konflikt, kein Serverfehler.
            return Results.Problem(fehler.Message, statusCode: StatusCodes.Status409Conflict);
        }
    }
}
