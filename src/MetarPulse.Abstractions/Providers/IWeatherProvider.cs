using MetarPulse.Core.Models;

namespace MetarPulse.Abstractions.Providers;

/// <summary>
/// Tüm METAR/TAF veri kaynakları bu interface'i implement eder.
/// Yeni bir kaynak eklemek için sadece bu interface'i implement et,
/// DI'a kaydet ve appsettings.json'a ekle. Mevcut koda sıfır dokunuş.
/// </summary>
public interface IWeatherProvider
{
    string ProviderName { get; }        // "AVWX", "CheckWX", "AWC", "MGM_RASAT"
    int Priority { get; }              // Fallback sırası (0 = en yüksek öncelik)
    bool IsEnabled { get; }            // Config'den okunur

    Task<Metar?> GetMetarAsync(string icaoCode, CancellationToken ct = default);
    Task<Taf?> GetTafAsync(string icaoCode, CancellationToken ct = default);
    Task<List<Metar>> GetMetarHistoryAsync(string icaoCode, int hours = 24, CancellationToken ct = default);
    Task<bool> HealthCheckAsync(CancellationToken ct = default);
}
