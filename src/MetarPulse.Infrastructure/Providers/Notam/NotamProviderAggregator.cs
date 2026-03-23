using MetarPulse.Abstractions.Providers;
using MetarPulse.Core.Enums;
using MetarPulse.Core.Models;
using Microsoft.Extensions.Logging;
using NotamModel = MetarPulse.Core.Models.Notam;

namespace MetarPulse.Infrastructure.Providers.Notam;

/// <summary>
/// Tüm aktif INotamProvider'ları paralel sorgular, sonuçları NotamId'ye göre birleştirir.
/// Aynı NotamId birden fazla kaynakta varsa en yüksek VfrImpact değerine sahip kayıt kazanır.
/// Bu strateji "ilk gelen kazanır"dan daha güvenlidir: tek kaynak OPS KISITMASI bir NOTAM'ı
/// kaçırsa bile diğer kaynak onu teslim eder.
/// </summary>
public class NotamProviderAggregator : INotamAggregator
{
    public string ProviderName => "NOTAM_AGGREGATOR";
    public bool IsEnabled => true;

    private readonly IReadOnlyList<INotamProvider> _providers;
    private readonly ILogger<NotamProviderAggregator> _logger;

    public NotamProviderAggregator(
        IEnumerable<INotamProvider> providers,
        ILogger<NotamProviderAggregator> logger)
    {
        _providers = providers.ToList().AsReadOnly();
        _logger = logger;
    }

    public IReadOnlyList<INotamProvider> GetAllProviders() => _providers;

    public async Task<IReadOnlyList<NotamModel>> GetNotamsAsync(string icao, CancellationToken ct = default)
    {
        var enabled = _providers.Where(p => p.IsEnabled).ToList();
        if (enabled.Count == 0) return [];

        _logger.LogDebug("NOTAM aggregator: {Count} provider paralel sorgulanıyor ({ICAO}).",
            enabled.Count, icao);

        var tasks = enabled.Select(p => FetchSafeAsync(p, icao, ct));
        var results = await Task.WhenAll(tasks);

        var merged = Merge(results.SelectMany(r => r));

        _logger.LogDebug("NOTAM aggregator: {ICAO} — {Total} toplam, {Merged} birleşik NOTAM.",
            icao, results.Sum(r => r.Count), merged.Count);

        return merged;
    }

    public async Task<IReadOnlyList<NotamModel>> GetNotamsAsync(IEnumerable<string> icaos, CancellationToken ct = default)
    {
        var tasks = icaos.Select(ic => GetNotamsAsync(ic.ToUpperInvariant(), ct));
        var results = await Task.WhenAll(tasks);
        return results.SelectMany(r => r).ToList();
    }

    public async Task<bool> HealthCheckAsync(CancellationToken ct = default)
    {
        var tasks = _providers.Where(p => p.IsEnabled)
                              .Select(p => p.HealthCheckAsync(ct));
        var results = await Task.WhenAll(tasks);
        return results.Any(r => r); // En az bir provider sağlıklıysa tamam
    }

    // ── Yardımcı metodlar ────────────────────────────────────────────────────

    private async Task<IReadOnlyList<NotamModel>> FetchSafeAsync(
        INotamProvider provider, string icao, CancellationToken ct)
    {
        try
        {
            var result = await provider.GetNotamsAsync(icao, ct);
            _logger.LogDebug("{Provider} → {ICAO}: {Count} NOTAM.",
                provider.ProviderName, icao, result.Count);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "{Provider} → {ICAO} başarısız, atlanıyor.",
                provider.ProviderName, icao);
            return [];
        }
    }

    /// <summary>
    /// NotamId'ye göre tekilleştirir.
    /// Aynı ID'de çakışma varsa en yüksek VfrImpact değeri korunur.
    /// </summary>
    private static IReadOnlyList<NotamModel> Merge(IEnumerable<NotamModel> all)
    {
        var byId = new Dictionary<string, NotamModel>(StringComparer.OrdinalIgnoreCase);

        foreach (var n in all)
        {
            if (string.IsNullOrWhiteSpace(n.NotamId)) continue;

            if (!byId.TryGetValue(n.NotamId, out var existing)
                || n.VfrImpact > existing.VfrImpact)
            {
                byId[n.NotamId] = n;
            }
        }

        return byId.Values
            .OrderByDescending(n => n.VfrImpact)
            .ThenBy(n => n.EffectiveFrom)
            .ToList()
            .AsReadOnly();
    }
}
