using MetarPulse.Abstractions.Providers;
using MetarPulse.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MetarPulse.Infrastructure.Providers.Weather;

/// <summary>
/// Bölge bazlı provider routing + fallback zinciri.
/// LT* → MGM_RASAT öncelikli; diğer istasyonlar → GlobalFallbackOrder.
/// Sağlık durumu in-memory olarak izlenir.
/// </summary>
public class RegionBasedProviderManager : IProviderManager
{
    private readonly IReadOnlyList<IWeatherProvider> _providers;
    private readonly IAirportDataProvider _airportProvider;
    private readonly WeatherProviderSettings _settings;
    private readonly ILogger<RegionBasedProviderManager> _logger;
    private readonly Dictionary<string, ProviderHealthStatus> _healthStatuses;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public RegionBasedProviderManager(
        IEnumerable<IWeatherProvider> providers,
        IAirportDataProvider airportProvider,
        IOptions<WeatherProviderSettings> settings,
        ILogger<RegionBasedProviderManager> logger)
    {
        _providers = providers.OrderBy(p => p.Priority).ToList().AsReadOnly();
        _airportProvider = airportProvider;
        _settings = settings.Value;
        _logger = logger;
        _healthStatuses = _providers.ToDictionary(
            p => p.ProviderName,
            p => new ProviderHealthStatus { ProviderName = p.ProviderName, IsHealthy = true });
    }

    // ── IProviderManager ─────────────────────────────────────────────────────

    public IWeatherProvider GetActiveWeatherProvider()
        => _providers.FirstOrDefault(p => p.IsEnabled)
           ?? throw new InvalidOperationException("Aktif weather provider bulunamadı.");

    public IReadOnlyList<IWeatherProvider> GetWeatherProviderChain(string icaoCode)
    {
        var fallbackOrder = GetFallbackOrder(icaoCode);
        return fallbackOrder
            .Select(name => _providers.FirstOrDefault(p =>
                p.ProviderName.Equals(name, StringComparison.OrdinalIgnoreCase) && p.IsEnabled))
            .Where(p => p != null)
            .Cast<IWeatherProvider>()
            .ToList()
            .AsReadOnly();
    }

    public IAirportDataProvider GetActiveAirportProvider() => _airportProvider;

    public async Task<Metar?> GetMetarWithFallbackAsync(string icaoCode, CancellationToken ct = default)
    {
        var chain    = GetWeatherProviderChain(icaoCode);
        var eligible = chain.Where(p => !IsCircuitOpen(p.ProviderName)).ToList();

        if (eligible.Count == 0)
        {
            _logger.LogError("Tüm provider'lar circuit-open durumunda: {ICAO}", icaoCode);
            return null;
        }

        // Tüm provider'ları paralel sorgula — en güncel ObservationTime'a sahip sonucu al
        var tasks   = eligible.Select(p => FetchMetarFromProviderAsync(p, icaoCode, ct));
        var results = await Task.WhenAll(tasks);

        var best = results
            .Where(r => r != null)
            .OrderByDescending(r => r!.ObservationTime)
            .FirstOrDefault();

        if (best == null)
            _logger.LogError("Tüm provider'lar başarısız: {ICAO}", icaoCode);
        else
            _logger.LogDebug("En güncel METAR seçildi: {Provider} → {ICAO} ({ObsTime:HH:mm}Z).",
                best.SourceProvider, icaoCode, best.ObservationTime);

        return best;
    }

    private async Task<Metar?> FetchMetarFromProviderAsync(
        IWeatherProvider provider, string icaoCode, CancellationToken ct)
    {
        try
        {
            var metar = await provider.GetMetarAsync(icaoCode, ct);
            if (metar != null)
            {
                RecordSuccess(provider.ProviderName);
                _logger.LogDebug("{Provider} → {ICAO} METAR ({ObsTime:HH:mm}Z).",
                    provider.ProviderName, icaoCode, metar.ObservationTime);
            }
            return metar;
        }
        catch (Exception ex)
        {
            RecordFailure(provider.ProviderName, ex.Message);
            _logger.LogWarning("{Provider} → {ICAO} hata: {Msg}", provider.ProviderName, icaoCode, ex.Message);
            return null;
        }
    }

    public async Task<Taf?> GetTafWithFallbackAsync(string icaoCode, CancellationToken ct = default)
    {
        var chain = GetWeatherProviderChain(icaoCode);
        foreach (var provider in chain)
        {
            if (IsCircuitOpen(provider.ProviderName)) continue;

            try
            {
                var taf = await provider.GetTafAsync(icaoCode, ct);
                if (taf != null)
                {
                    RecordSuccess(provider.ProviderName);
                    return taf;
                }
            }
            catch (Exception ex)
            {
                RecordFailure(provider.ProviderName, ex.Message);
                _logger.LogWarning("{Provider} → {ICAO} TAF hata: {Msg}", provider.ProviderName, icaoCode, ex.Message);
            }
        }

        return null;
    }

    public async Task SetProviderPriorityAsync(string providerName, int newPriority)
    {
        await _lock.WaitAsync();
        try
        {
            var provider = _providers.FirstOrDefault(p =>
                p.ProviderName.Equals(providerName, StringComparison.OrdinalIgnoreCase));
            if (provider == null)
                throw new ArgumentException($"Provider bulunamadı: {providerName}");

            // Config'deki priority'yi güncelle (in-memory)
            if (_settings.Providers.TryGetValue(providerName, out var config))
                config.Priority = newPriority;
        }
        finally { _lock.Release(); }
    }

    public async Task EnableProviderAsync(string providerName, bool enabled)
    {
        await _lock.WaitAsync();
        try
        {
            if (_settings.Providers.TryGetValue(providerName, out var config))
                config.Enabled = enabled;

            if (_healthStatuses.TryGetValue(providerName, out var status))
                status.IsHealthy = enabled;
        }
        finally { _lock.Release(); }
    }

    public IReadOnlyList<IWeatherProvider> GetAllWeatherProviders() => _providers;

    public async Task ReorderAsync(IEnumerable<string> globalOrder, IEnumerable<string> turkeyOrder)
    {
        await _lock.WaitAsync();
        try
        {
            _settings.GlobalFallbackOrder = globalOrder.ToList();
            if (_settings.RegionOverrides.TryGetValue("TR", out var tr))
                tr.FallbackOrder = turkeyOrder.ToList();

            // Priority değerlerini de güncelle
            var global = _settings.GlobalFallbackOrder;
            for (int i = 0; i < global.Count; i++)
                if (_settings.Providers.TryGetValue(global[i], out var cfg))
                    cfg.Priority = i;
        }
        finally { _lock.Release(); }
    }

    public IReadOnlyList<ProviderHealthStatus> GetHealthStatuses()
        => _healthStatuses.Values.ToList().AsReadOnly();

    // ── Yardımcı metodlar ────────────────────────────────────────────────────

    private List<string> GetFallbackOrder(string icaoCode)
    {
        // Bölge override kontrolü — prefix bazlı
        foreach (var (_, region) in _settings.RegionOverrides)
            if (region.IcaoPrefixes.Any(prefix =>
                icaoCode.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
                return region.FallbackOrder;

        return _settings.GlobalFallbackOrder;
    }

    private bool IsCircuitOpen(string providerName)
    {
        if (!_healthStatuses.TryGetValue(providerName, out var status)) return false;
        if (!status.IsCircuitOpen) return false;

        // 30 saniye sonra devre tekrar kapatılır (basit in-memory circuit breaker)
        if (status.LastChecked < DateTime.UtcNow.AddSeconds(-30))
        {
            status.IsCircuitOpen = false;
            status.ConsecutiveFailures = 0;
        }

        return status.IsCircuitOpen;
    }

    private void RecordSuccess(string providerName)
    {
        if (!_healthStatuses.TryGetValue(providerName, out var status)) return;
        status.IsHealthy = true;
        status.IsCircuitOpen = false;
        status.ConsecutiveFailures = 0;
        status.LastSuccess = DateTime.UtcNow;
        status.LastChecked = DateTime.UtcNow;
        status.LastError = null;
    }

    private void RecordFailure(string providerName, string error)
    {
        if (!_healthStatuses.TryGetValue(providerName, out var status)) return;
        status.ConsecutiveFailures++;
        status.LastError = error;
        status.LastChecked = DateTime.UtcNow;

        // 5 ardışık hatadan sonra devre açılır
        if (status.ConsecutiveFailures >= 5)
        {
            status.IsHealthy = false;
            status.IsCircuitOpen = true;
            _logger.LogWarning("{Provider} circuit breaker açıldı ({Count} ardışık hata).",
                providerName, status.ConsecutiveFailures);
        }
    }
}
