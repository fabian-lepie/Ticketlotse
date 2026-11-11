using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using NUnit.Framework;
using Ticketlotse.Api;
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
}
