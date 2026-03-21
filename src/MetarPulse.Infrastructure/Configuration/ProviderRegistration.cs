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

        return services;
    }
}
