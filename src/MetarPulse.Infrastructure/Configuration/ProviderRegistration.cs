using MetarPulse.Abstractions.Providers;
using MetarPulse.Infrastructure.Providers.Notam;
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
        // MGM Hezarfen Rasat: Next.js SSR sayfası, browser User-Agent gerekli
        services.AddHttpClient("MGM_RASAT", client =>
        {
            client.DefaultRequestHeaders.Add("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");
            client.DefaultRequestHeaders.Add("Accept",
                "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
        });
        services.AddHttpClient("AVWX");
        services.AddHttpClient("CheckWX");
        services.AddHttpClient("AWC");
        services.AddHttpClient("NOAA_TGFTP");

        // AirportSync: büyük CSV dosyaları (airports.csv ~7MB, runways.csv ~4MB) için uzun timeout
        services.AddHttpClient("AirportSync", client =>
            client.Timeout = TimeSpan.FromMinutes(5));

        // Weather Provider'lar — Singleton: enable/disable state değişiklikleri tüm scope'larda anında geçerli
        services.AddSingleton<IWeatherProvider, MgmRasatWeatherProvider>();
        services.AddSingleton<IWeatherProvider, AvwxWeatherProvider>();
        services.AddSingleton<IWeatherProvider, CheckWxWeatherProvider>();
        services.AddSingleton<IWeatherProvider, AwcWeatherProvider>();
        services.AddSingleton<IWeatherProvider, NoaaTgftpWeatherProvider>();

        // Provider Manager — Singleton: provider chain her zaman güncel state'i yansıtır
        services.AddSingleton<IProviderManager, RegionBasedProviderManager>();

        // ── NOTAM Provider'lar ──────────────────────────────────────────────
        // Her biri INotamProvider olarak kaydedilir; aggregator hepsini toplar.
        services.AddSingleton<INotamProvider, AviationWeatherNotamProvider>();
        services.AddSingleton<INotamProvider, FaaNotamSearchProvider>();
        services.AddSingleton<INotamProvider, AutorouterNotamProvider>();

        // Aggregator: NotamController ve NotamPollingService bu arayüzü inject eder.
        // IEnumerable<INotamProvider> → yukarıdaki üç provider'ı alır (aggregator kendisi hariç).
        services.AddSingleton<INotamAggregator, NotamProviderAggregator>();

        return services;
    }
}
