using Ticketlotse.Api;
using Ticketlotse.Application;
using Ticketlotse.Domain;
using Ticketlotse.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Einheitliche Fehlerausgabe als ProblemDetails (RFC 9457).
builder.Services.AddProblemDetails();

// Ablage noch im Arbeitsspeicher – in Etappe 4 ersetzt EF Core diese Zeile.
builder.Services.AddSingleton<ITicketRepository, TicketRepositoryImArbeitsspeicher>();
builder.Services.AddSingleton<ITicketKlassifizierer, KeineKiAngebunden>();

builder.Services.AddScoped<Ticketverwaltung>();

builder.Services.AddExceptionHandler<UnerwarteteFehler>();

var app = builder.Build();
app.UseExceptionHandler();

app.MapTicketEndpunkte();

app.Run();

// Damit die Integrationstests die Anwendung hochfahren können.
public partial class Program;

