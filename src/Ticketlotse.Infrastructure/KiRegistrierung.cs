using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Ticketlotse.Application;

namespace Ticketlotse.Infrastructure;

public static class KiRegistrierung
{
    /// <summary>
    /// Bindet den KI-Klassifizierer ein. Ohne Zugangsschlüssel bleibt es bei
    /// <see cref="KeineKiAngebunden"/> – die Anwendung läuft dann ohne KI weiter.
    /// </summary>
    public static IServiceCollection KiKlassifiziererHinzufuegen(
        this IServiceCollection dienste,
        IConfiguration konfiguration)
    {
        var abschnitt = konfiguration.GetSection("Anthropic");
        dienste.Configure<ClaudeEinstellungen>(abschnitt);

        var schluessel = abschnitt["ApiKey"];
        if (string.IsNullOrWhiteSpace(schluessel))
            return dienste.AddSingleton<ITicketKlassifizierer, KeineKiAngebunden>();

        var einstellungen = abschnitt.Get<ClaudeEinstellungen>() ?? new ClaudeEinstellungen();

        dienste.AddHttpClient<ITicketKlassifizierer, ClaudeTicketKlassifizierer>(client =>
        {
            client.BaseAddress = new Uri("https://api.anthropic.com/");
            client.Timeout = TimeSpan.FromSeconds(einstellungen.ZeitueberschreitungSekunden);
            client.DefaultRequestHeaders.Add("x-api-key", schluessel);
            client.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
        });

        return dienste;
    }
}