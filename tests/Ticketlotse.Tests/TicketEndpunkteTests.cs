using System.Net;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Ticketlotse.Api;
using Ticketlotse.Application;
using Ticketlotse.Domain;


namespace Ticketlotse.Tests;

public class TicketEndpunkteTests
{
    // Jeder Test bekommt eine frische Anwendung – der Speicher ist damit leer.
    private WebApplicationFactory<Program> _anwendung = null!;
    private HttpClient _client = null!;

    [SetUp]
    public void SetUp()
    {
        _anwendung = new WebApplicationFactory<Program>();
        _client = _anwendung.CreateClient();
    }

    [TearDown]
    public void TearDown()
    {
        _client.Dispose();
        _anwendung.Dispose();
    }

    private static TicketAnlegenAnfrage Anfrage(string text = "Mein Abschlag ist zu hoch.") =>
        new("kunde@example.de", "Abschlag", text);

    private async Task<TicketAntwort> TicketAnlegen(string text = "Mein Abschlag ist zu hoch.")
    {
        var antwort = await _client.PostAsJsonAsync("/tickets", Anfrage(text));
        antwort.EnsureSuccessStatusCode();
        return (await antwort.Content.ReadFromJsonAsync<TicketAntwort>())!;
    }

    [Test]
    public async Task POST_tickets_legt_ein_neues_Ticket_an()
    {
        var antwort = await _client.PostAsJsonAsync("/tickets", Anfrage());

        Assert.That(antwort.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        var ticket = (await antwort.Content.ReadFromJsonAsync<TicketAntwort>())!;
        Assert.That(ticket.Id, Is.Not.EqualTo(Guid.Empty));
        Assert.That(ticket.Status, Is.EqualTo(nameof(TicketStatus.Neu)));
        Assert.That(ticket.Kategorie, Is.Null, "Die Einordnung kommt erst später durch die KI.");
        Assert.That(antwort.Headers.Location!.ToString(), Does.Contain(ticket.Id.ToString()));
    }

    [Test]
    public async Task POST_tickets_ohne_Text_wird_abgelehnt()
    {
        var antwort = await _client.PostAsJsonAsync("/tickets", Anfrage(text: "   "));

        Assert.That(antwort.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task GET_ticket_gibt_das_angelegte_Ticket_zurueck()
    {
        var angelegt = await TicketAnlegen();

        var ticket = await _client.GetFromJsonAsync<TicketAntwort>($"/tickets/{angelegt.Id}");

        Assert.That(ticket!.Id, Is.EqualTo(angelegt.Id));
        Assert.That(ticket.Absender, Is.EqualTo("kunde@example.de"));
    }

    [Test]
    public async Task GET_unbekanntes_Ticket_gibt_404()
    {
        var antwort = await _client.GetAsync($"/tickets/{Guid.NewGuid()}");

        Assert.That(antwort.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task GET_tickets_filtert_nach_Status()
    {
        var angelegt = await TicketAnlegen();

        var neue = await _client.GetFromJsonAsync<TicketAntwort[]>("/tickets?status=Neu");
        var freigegebene = await _client.GetFromJsonAsync<TicketAntwort[]>("/tickets?status=Freigegeben");

        Assert.That(neue!.Select(t => t.Id), Is.EquivalentTo(new[] { angelegt.Id }));
        Assert.That(freigegebene, Is.Empty);
    }

    [Test]
    public async Task GET_tickets_mit_unbekanntem_Status_wird_abgelehnt()
    {
        var antwort = await _client.GetAsync("/tickets?status=Quatsch");

        Assert.That(antwort.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [TestCase("99")]
    [TestCase("2")]
    public async Task GET_tickets_mit_Status_als_Zahl_wird_abgelehnt(string status)
    {
        var antwort = await _client.GetAsync($"/tickets?status={status}");

        Assert.That(antwort.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [TestCase("neu")]
    [TestCase("NEU")]
    public async Task GET_tickets_akzeptiert_den_Status_unabhaengig_von_Gross_und_Kleinschreibung(string status)
    {
        var antwort = await _client.GetAsync($"/tickets?status={status}");

        Assert.That(antwort.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task PUT_freigabe_eines_unbekannten_Tickets_gibt_404()
    {
        var antwort = await _client.PutAsync($"/tickets/{Guid.NewGuid()}/freigabe", null);

        Assert.That(antwort.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task PUT_freigabe_vor_der_Einordnung_gibt_409()
    {
        var angelegt = await TicketAnlegen();

        var antwort = await _client.PutAsync($"/tickets/{angelegt.Id}/freigabe", null);

        Assert.That(antwort.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
    }

    [Test]
    public async Task POST_tickets_mit_kaputtem_JSON_wird_abgelehnt()
    {
        using var inhalt = new StringContent("{\"absender\": ", Encoding.UTF8, "application/json");

        var antwort = await _client.PostAsync("/tickets", inhalt);

        Assert.That(antwort.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task POST_tickets_ohne_Body_wird_abgelehnt()
    {
        var antwort = await _client.PostAsync("/tickets", null);

        Assert.That(antwort.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }


    [Test]
    public async Task POST_tickets_leerer_Body_wird_abgelehnt()
    {
        using var inhalt = new StringContent("", Encoding.UTF8, "application/json");

        var antwort = await _client.PostAsync("/tickets", inhalt);

        Assert.That(antwort.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        // keine Fehlerdetails in der Antwort
        var koerper = await antwort.Content.ReadAsStringAsync();
        Assert.That(koerper, Does.Not.Contain("System.Text.Json"));
    }

    [Test]
    public async Task POST_tickets_mit_falschem_Content_Type_wird_abgelehnt()
    {
        using var inhalt = new StringContent("Hallo", Encoding.UTF8, "text/plain");

        var antwort = await _client.PostAsync("/tickets", inhalt);

        Assert.That(antwort.StatusCode, Is.EqualTo(HttpStatusCode.UnsupportedMediaType));
    }

    [Test]
    public async Task GET_ticket_mit_einer_Id_die_keine_Guid_ist_gibt_404()
    {
        var antwort = await _client.GetAsync("/tickets/abc");

        Assert.That(antwort.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task PUT_freigabe_mit_einer_Id_die_keine_Guid_ist_gibt_404()
    {
        var antwort = await _client.PutAsync("/tickets/abc/freigabe", null);

        Assert.That(antwort.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    private sealed class StolperndesRepository : ITicketRepository
    {
        public Task HinzufuegenAsync(Ticket ticket, CancellationToken abbruch = default) =>
            throw new IOException("Datenspeicher nicht erreichbar");

        public Task<Ticket?> FindenAsync(Guid id, CancellationToken abbruch = default) =>
            throw new IOException("Datenspeicher nicht erreichbar");

        public Task<IReadOnlyList<Ticket>> SuchenAsync(TicketStatus? status = null, CancellationToken abbruch = default) =>
            throw new IOException("Datenspeicher nicht erreichbar");

        public Task AktualisierenAsync(Ticket ticket, CancellationToken abbruch = default) =>
            throw new IOException("Datenspeicher nicht erreichbar");
    }

    [Test]
    public async Task Unerwartete_Fehler_werden_als_500_ohne_Interna_beantwortet()
    {
        using var anwendung = _anwendung.WithWebHostBuilder(aufbau =>
            aufbau.ConfigureServices(dienste =>
                dienste.AddSingleton<ITicketRepository, StolperndesRepository>()));
        using var client = anwendung.CreateClient();

        var antwort = await client.PostAsJsonAsync("/tickets", Anfrage());
        var koerper = await antwort.Content.ReadAsStringAsync();

        Assert.That(antwort.StatusCode, Is.EqualTo(HttpStatusCode.InternalServerError));
        Assert.That(koerper, Does.Not.Contain("StolperndesRepository"));
        Assert.That(koerper, Does.Not.Contain("Datenspeicher nicht erreichbar"));
    }

    private sealed class ProtokollSammler : ILoggerProvider
    {
        private readonly List<(LogLevel Level, string Kategorie)> _zeilen = [];

        public IReadOnlyList<(LogLevel Level, string Kategorie)> Zeilen
        {
            get { lock (_zeilen) return _zeilen.ToList(); }
        }

        public ILogger CreateLogger(string categoryName) => new Sammler(this, categoryName);

        public void Dispose() { }

        private sealed class Sammler(ProtokollSammler besitzer, string kategorie) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
                Exception? exception, Func<TState, Exception?, string> formatter)
            {
                lock (besitzer._zeilen) besitzer._zeilen.Add((logLevel, kategorie));
            }
        }
    }

    private async Task<IReadOnlyList<(LogLevel Level, string Kategorie)>> KaputtesJsonSenden(string umgebung)
    {
        var sammler = new ProtokollSammler();
        using var anwendung = _anwendung.WithWebHostBuilder(aufbau =>
        {
            aufbau.UseEnvironment(umgebung);
            aufbau.ConfigureLogging(protokoll => protokoll.AddProvider(sammler));
        });
        using var client = anwendung.CreateClient();
        using var inhalt = new StringContent("{\"absender\": ", Encoding.UTF8, "application/json");

        var antwort = await client.PostAsync("/tickets", inhalt);

        Assert.That(antwort.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest),
                    "Der Statuscode muss in jeder Umgebung gleich sein.");
        return sammler.Zeilen;
    }

    [Test]
    public async Task Ungueltige_Anfragen_werden_in_Development_protokolliert()
    {
        var zeilen = await KaputtesJsonSenden("Development");

        Assert.That(zeilen.Any(z => z.Level == LogLevel.Warning
                                 && z.Kategorie == "Ticketlotse.Api.UnerwarteteFehler"), Is.True);
    }

    [Test]
    public async Task Ungueltige_Anfragen_werden_in_Produktion_nicht_protokolliert()
    {
        var zeilen = await KaputtesJsonSenden("Production");

        Assert.That(zeilen.Any(z => z.Level >= LogLevel.Warning
                                 && z.Kategorie == "Ticketlotse.Api.UnerwarteteFehler"), Is.False);
    }
}
