using MetarPulse.Core.Models;

namespace MetarPulse.Abstractions.Providers;

/// <summary>
/// Provider seçim, fallback ve sağlık yönetimi.
/// Bölge bazlı routing (Türkiye → MGM, Global → AVWX) buradan yönetilir.
/// </summary>
public interface IProviderManager
{
    IWeatherProvider GetActiveWeatherProvider();
    IReadOnlyList<IWeatherProvider> GetWeatherProviderChain(string icaoCode);
    IAirportDataProvider GetActiveAirportProvider();

    Task<Metar?> GetMetarWithFallbackAsync(string icaoCode, CancellationToken ct = default);
    Task<Taf?> GetTafWithFallbackAsync(string icaoCode, CancellationToken ct = default);

    Task SetProviderPriorityAsync(string providerName, int newPriority);
    Task EnableProviderAsync(string providerName, bool enabled);
    Task ReorderAsync(IEnumerable<string> globalOrder, IEnumerable<string> turkeyOrder);

    IReadOnlyList<IWeatherProvider> GetAllWeatherProviders();
    IReadOnlyList<ProviderHealthStatus> GetHealthStatuses();
}
