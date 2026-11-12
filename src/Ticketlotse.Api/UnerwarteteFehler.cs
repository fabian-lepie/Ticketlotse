using Microsoft.AspNetCore.Diagnostics;

namespace Ticketlotse.Api;

/// <summary>
/// Fängt alles, was die Endpunkte nicht selbst behandeln: protokolliert es
/// und antwortet neutral mit 500, ohne Interna preiszugeben.
/// </summary>
internal sealed class UnerwarteteFehler(
    ILogger<UnerwarteteFehler> loggerFabrik,
    IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext kontext,
        Exception fehler,
        CancellationToken abbruch)
    {
        // Ein Abbruch durch den Aufrufer ist kein Serverfehler.
        if (fehler is OperationCanceledException && kontext.RequestAborted.IsCancellationRequested)
            return false;

        // Fehler beim Lesen der Anfrage bringen ihren Statuscode schon mit (400, 415, …).
        if (fehler is BadHttpRequestException schlechteAnfrage)
        {
            loggerFabrik.LogWarning("Ungültige Anfrage bei {Methode} {Pfad}: {Grund}",
                kontext.Request.Method, kontext.Request.Path, fehler.Message);

            kontext.Response.StatusCode = schlechteAnfrage.StatusCode;
            return await problemDetails.TryWriteAsync(new ProblemDetailsContext { HttpContext = kontext });
        }


        loggerFabrik.LogError(fehler, "Unerwarteter Fehler bei {Methode} {Pfad}",
            kontext.Request.Method, kontext.Request.Path);

        kontext.Response.StatusCode = StatusCodes.Status500InternalServerError;

        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = kontext,
            ProblemDetails =
            {
                Title = "Unerwarteter Fehler",
                Detail = "Die Anfrage konnte nicht verarbeitet werden."
            }
        });
    }
}