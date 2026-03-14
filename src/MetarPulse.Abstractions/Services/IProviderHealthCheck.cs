using MetarPulse.Core.Models;

namespace MetarPulse.Abstractions.Services;

public interface IProviderHealthCheck
{
    Task<ProviderHealthStatus> CheckAsync(string providerName, CancellationToken ct = default);
    Task<IReadOnlyList<ProviderHealthStatus>> CheckAllAsync(CancellationToken ct = default);
}
