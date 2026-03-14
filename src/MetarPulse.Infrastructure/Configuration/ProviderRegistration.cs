using MetarPulse.Abstractions.Providers;
using MetarPulse.Infrastructure.Providers.Weather;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MetarPulse.Infrastructure.Configuration;

/// <summary>
/// Weather ve Airport provider'larının DI kaydı.
/// Yeni bir provider eklemek için sadece buraya bir satır ekle.
/// </summary>
public static class ProviderRegistration
{
    public static IServiceCollection AddWeatherProviders(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<WeatherProviderSettings>(options =>
            configuration.GetSection("WeatherProviders").Bind(options));

        services.Configure<AirportProviderSettings>(options =>
            configuration.GetSection("AirportDataProviders").Bind(options));

        // Named HttpClient'lar — her provider kendi timeout/header yapılandırmasına sahip
        services.AddHttpClient("MGM_RASAT");
        services.AddHttpClient("AVWX");
        services.AddHttpClient("CheckWX");
        services.AddHttpClient("AWC");

        // AirportSync: büyük CSV dosyaları (airports.csv ~7MB, runways.csv ~4MB) için uzun timeout
        services.AddHttpClient("AirportSync", client =>
            client.Timeout = TimeSpan.FromMinutes(5));

        // Weather Provider'lar — IEnumerable<IWeatherProvider> üzerinden inject edilir
        services.AddScoped<IWeatherProvider, MgmRasatWeatherProvider>();
        services.AddScoped<IWeatherProvider, AvwxWeatherProvider>();
        services.AddScoped<IWeatherProvider, CheckWxWeatherProvider>();
        services.AddScoped<IWeatherProvider, AwcWeatherProvider>();

        // Provider Manager — bölge bazlı routing + fallback zinciri
        services.AddScoped<IProviderManager, RegionBasedProviderManager>();

        return services;
    }
}
