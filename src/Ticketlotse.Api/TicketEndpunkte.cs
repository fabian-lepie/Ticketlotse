using Microsoft.AspNetCore.Http.HttpResults;
using Ticketlotse.Application;
using Ticketlotse.Domain;
using static Ticketlotse.Api.TicketAntwort;

namespace Ticketlotse.Api;

public static class TicketEndpunkte
{
    public static void MapTicketEndpunkte(this WebApplication app)
    {
        var tickets = app.MapGroup("/tickets");

        tickets.MapPost("", Anlegen);
        tickets.MapGet("", Suchen);
        tickets.MapGet("/{id:guid}", Abrufen);
        tickets.MapPost("/{id:guid}/einordnung", ErneutEinordnen);
        tickets.MapPut("/{id:guid}/klassifizierung", Korrigieren);
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
            if (!IstBekannterWert<TicketStatus>(status, out var erkannt))
            {
                return TypedResults.Problem(
                    $"Unbekannter Status \"{status}\". Möglich sind: {ErlaubteWerte<TicketStatus>()}.",
                    statusCode: StatusCodes.Status400BadRequest);
            }
            filter = erkannt;
        }

        var treffer = await verwaltung.SuchenAsync(filter, abbruch);
        return TypedResults.Ok(treffer.Select(TicketAntwort.Von));
    }

    private static bool IstBekannterWert<TAufzaehlung>(string? eingabe, out TAufzaehlung wert)
      where TAufzaehlung : struct, Enum
    {
        var name = Enum.GetNames<TAufzaehlung>()
            .FirstOrDefault(n => string.Equals(n, eingabe, StringComparison.OrdinalIgnoreCase));
        if (name is null)
        {
            wert = default;
            return false;
        }
        wert = Enum.Parse<TAufzaehlung>(name);
        return true;
    }

    private static string ErlaubteWerte<TAufzaehlung>() where TAufzaehlung : struct, Enum =>
        string.Join(", ", Enum.GetNames<TAufzaehlung>());

    private static async Task<Results<Ok<TicketDetailAntwort>, NotFound>> Abrufen(
     Guid id,
     Ticketverwaltung verwaltung,
     CancellationToken abbruch)
    {
        var ticket = await verwaltung.FindenAsync(id, abbruch);
        return ticket is null ? TypedResults.NotFound() : TypedResults.Ok(TicketDetailAntwort.Von(ticket));
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

    private static async Task<Results<Ok<TicketAntwort>, NotFound, ProblemHttpResult>> ErneutEinordnen(
    Guid id,
    Ticketverwaltung verwaltung,
    ILoggerFactory loggerFabrik,
    CancellationToken abbruch)
    {
        var protokoll = loggerFabrik.CreateLogger(typeof(TicketEndpunkte));

        try
        {
            var ticket = await verwaltung.ErneutEinordnenAsync(id, abbruch);
            if (ticket is null)
                return TypedResults.NotFound();

            if (ticket.AktuelleEinordnung is null)
            {
                // Die KI hat auch beim zweiten Versuch nicht geantwortet; das Ticket bleibt liegen.
                protokoll.LogWarning("Erneute Einordnung von Ticket {TicketId} ohne Ergebnis", id);
                return TypedResults.Problem(
                    "Die KI konnte das Ticket nicht einordnen. Bitte später erneut versuchen.",
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }

            return TypedResults.Ok(TicketAntwort.Von(ticket));
        }
        catch (InvalidOperationException fehler)
        {
            // Das Ticket ist bereits eingeordnet – eine Änderung läuft über die Korrektur.
            protokoll.LogWarning("Erneute Einordnung von Ticket {TicketId} abgelehnt: {Grund}", id, fehler.Message);
            return TypedResults.Problem(fehler.Message, statusCode: StatusCodes.Status409Conflict);
        }
    }

    private static async Task<Results<Ok<TicketAntwort>, NotFound, ProblemHttpResult>> Korrigieren(
        Guid id,
        KorrekturAnfrage anfrage,
        Ticketverwaltung verwaltung,
        ILoggerFactory loggerFabrik,
        CancellationToken abbruch)
    {
        var protokoll = loggerFabrik.CreateLogger(typeof(TicketEndpunkte));

        if (!IstBekannterWert<Kategorie>(anfrage.Kategorie, out var kategorie))
            return TypedResults.Problem(
                $"Unbekannte Kategorie \"{anfrage.Kategorie}\". Möglich sind: {ErlaubteWerte<Kategorie>()}.",
                statusCode: StatusCodes.Status400BadRequest);

        if (!IstBekannterWert<Dringlichkeit>(anfrage.Dringlichkeit, out var dringlichkeit))
            return TypedResults.Problem(
                $"Unbekannte Dringlichkeit \"{anfrage.Dringlichkeit}\". Möglich sind: {ErlaubteWerte<Dringlichkeit>()}.",
                statusCode: StatusCodes.Status400BadRequest);

        try
        {
            var ticket = await verwaltung.KorrigierenAsync(
                id, kategorie, dringlichkeit, anfrage.Begruendung, anfrage.Bearbeiter, abbruch);

            return ticket is null ? TypedResults.NotFound() : TypedResults.Ok(TicketAntwort.Von(ticket));
        }
        catch (ArgumentException fehler)
        {
            // Begründung oder Bearbeiter fehlen – das prüft die Einordnung selbst.
            protokoll.LogWarning("Korrektur von Ticket {TicketId} abgelehnt: {Grund}", id, fehler.Message);
            return TypedResults.Problem(fehler.Message, statusCode: StatusCodes.Status400BadRequest);
        }
        catch (InvalidOperationException fehler)
        {
            // Das Ticket ist noch nicht eingeordnet oder schon freigegeben.
            protokoll.LogWarning("Korrektur von Ticket {TicketId} abgelehnt: {Grund}", id, fehler.Message);
            return TypedResults.Problem(fehler.Message, statusCode: StatusCodes.Status409Conflict);
        }
    }
}