using MetarPulse.Core.Models;

namespace MetarPulse.Abstractions.Providers;

/// <summary>
/// NOTAM veri kaynakları bu interface'i implement eder.
/// Yeni bir kaynak eklemek için sadece bu interface'i implement et ve DI'a kaydet.
/// </summary>
public interface INotamProvider
{
    string ProviderName { get; }
    bool IsEnabled { get; }

    /// <summary>Verilen ICAO için aktif NOTAM listesini döner.</summary>
    Task<IReadOnlyList<Notam>> GetNotamsAsync(string icao, CancellationToken ct = default);

    /// <summary>Birden fazla ICAO için NOTAM listesini döner.</summary>
    Task<IReadOnlyList<Notam>> GetNotamsAsync(IEnumerable<string> icaos, CancellationToken ct = default);

    /// <summary>Provider sağlık kontrolü.</summary>
    Task<bool> HealthCheckAsync(CancellationToken ct = default);
}
