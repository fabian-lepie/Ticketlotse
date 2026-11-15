using System.Net;
using System.Text;

namespace Ticketlotse.Tests;

/// <summary>
/// Schiebt dem HttpClient eine feste Antwort unter – ohne Netzwerk.
/// </summary>
internal sealed class FakeHttpHandler : HttpMessageHandler
{
    private readonly HttpStatusCode _status;
    private readonly string _inhalt;
    private readonly bool _zeitueberschreitung;

    public int Aufrufe { get; private set; }
    public string? LetzterRumpf { get; private set; }

    private FakeHttpHandler(HttpStatusCode status, string inhalt, bool zeitueberschreitung)
    {
        _status = status;
        _inhalt = inhalt;
        _zeitueberschreitung = zeitueberschreitung;
    }

    public static FakeHttpHandler Antwortet(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status, json, zeitueberschreitung: false);

    public static FakeHttpHandler Scheitert(HttpStatusCode status) =>
        new(status, """{"type":"error","error":{"type":"api_error","message":"Fehler"}}""", false);

    /// <summary>Wie eine abgelaufene Zeitüberschreitung des HttpClient.</summary>
    public static FakeHttpHandler LaeuftInDieZeitueberschreitung() =>
        new(HttpStatusCode.OK, "", zeitueberschreitung: true);

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Aufrufe++;
        LetzterRumpf = request.Content is null
            ? null
            : await request.Content.ReadAsStringAsync(cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();

        if (_zeitueberschreitung)
            throw new TaskCanceledException("Die Zeitüberschreitung ist abgelaufen.");

        return new HttpResponseMessage(_status)
        {
            Content = new StringContent(_inhalt, Encoding.UTF8, "application/json"),
        };
    }
}