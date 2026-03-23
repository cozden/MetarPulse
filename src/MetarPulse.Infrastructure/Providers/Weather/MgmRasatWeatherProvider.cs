using System.Text.Json;
using System.Text.RegularExpressions;
using MetarPulse.Abstractions.Providers;
using MetarPulse.Core.Models;
using MetarPulse.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MetarPulse.Infrastructure.Providers.Weather;

/// <summary>
/// MGM Hezarfen Rasat — Türkiye meydanları için birincil kaynak.
/// LT* prefix'li ICAO kodları için öncelikli kullanılır.
/// Endpoint: GET {BaseUrl}/result?stations={ICAO}&amp;obsType=1&amp;obsType=2&amp;hours=0
/// __NEXT_DATA__ JSON bloğundan METAR/TAF ham metni çıkarılır.
/// observationType: 4=METAR/SPECI, 6=TAF
/// </summary>
public class MgmRasatWeatherProvider : BaseWeatherProvider
{
    public override string ProviderName => "MGM_RASAT";

    public MgmRasatWeatherProvider(
        IHttpClientFactory httpClientFactory,
        IOptions<WeatherProviderSettings> settings,
        ILogger<MgmRasatWeatherProvider> logger)
        : base(
            httpClientFactory.CreateClient("MGM_RASAT"),
            settings.Value.Providers.GetValueOrDefault("MGM_RASAT") ?? new ProviderConfig(),
            logger)
    {
    }

    public override async Task<Metar?> GetMetarAsync(string icaoCode, CancellationToken ct = default)
    {
        if (!IsTurkishStation(icaoCode))
        {
            _logger.LogDebug("MGM_RASAT yalnızca LT* meydanlarını destekler: {ICAO}", icaoCode);
            return null;
        }

        try
        {
            var (metar, _) = await FetchLatestAsync(icaoCode, ct);
            return metar != null ? MetarParser.Parse(metar, ProviderName) : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MGM_RASAT METAR parse hatası: {ICAO}", icaoCode);
            return null;
        }
    }

    public override async Task<Taf?> GetTafAsync(string icaoCode, CancellationToken ct = default)
    {
        if (!IsTurkishStation(icaoCode)) return null;

        try
        {
            var (_, taf) = await FetchLatestAsync(icaoCode, ct);
            return taf != null ? TafParser.Parse(taf, ProviderName) : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MGM_RASAT TAF parse hatası: {ICAO}", icaoCode);
            return null;
        }
    }

    public override async Task<List<Metar>> GetMetarHistoryAsync(
        string icaoCode, int hours = 24, CancellationToken ct = default)
    {
        if (!IsTurkishStation(icaoCode)) return [];

        // Hezarfen geçmişi hours parametresiyle destekliyor
        try
        {
            var url = $"{Config.BaseUrl}/result?stations={icaoCode.ToUpper()}&obsType=1&hours={hours}";
            var response = await GetWithResilienceAsync(url, ct);
            if (response == null || !response.IsSuccessStatusCode) return [];

            var html = await response.Content.ReadAsStringAsync(ct);
            var items = ExtractDataLastItems(html);

            return items
                .Where(x => x.Type is 4 or 5)           // 4=METAR, 5=SPECI
                .Select(x => x.Text)
                .Select(raw => MetarParser.Parse(raw, ProviderName))
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MGM_RASAT geçmiş parse hatası: {ICAO}", icaoCode);
            var current = await GetMetarAsync(icaoCode, ct);
            return current != null ? [current] : [];
        }
    }

    public override async Task<bool> HealthCheckAsync(CancellationToken ct = default)
    {
        try
        {
            var metar = await GetMetarAsync("LTFM", ct);
            return metar != null;
        }
        catch
        {
            return false;
        }
    }

    // ── Yardımcılar ──────────────────────────────────────────────────────────

    private static bool IsTurkishStation(string icaoCode)
        => icaoCode.Length >= 2 && icaoCode.StartsWith("LT", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Tek HTTP isteğiyle hem METAR hem TAF metnini döner.
    /// </summary>
    private async Task<(string? MetarRaw, string? TafRaw)> FetchLatestAsync(
        string icaoCode, CancellationToken ct)
    {
        var url = $"{Config.BaseUrl}/result?stations={icaoCode.ToUpper()}&obsType=1&obsType=2&hours=0";
        var response = await GetWithResilienceAsync(url, ct);
        if (response == null || !response.IsSuccessStatusCode) return (null, null);

        var html = await response.Content.ReadAsStringAsync(ct);
        var items = ExtractDataLastItems(html);

        // observationType: 4=METAR, 5=SPECI, 6=TAF
        var metarRaw = items.FirstOrDefault(x => x.Type is 4 or 5)?.Text;
        var tafRaw   = items.FirstOrDefault(x => x.Type == 6)?.Text;

        return (metarRaw, tafRaw);
    }

    private static readonly Regex NextDataRegex =
        new(@"<script id=""__NEXT_DATA__"" type=""application/json"">(.*?)</script>",
            RegexOptions.Singleline | RegexOptions.Compiled);

    private record ObsItem(int Type, string Text);

    private static List<ObsItem> ExtractDataLastItems(string html)
    {
        var match = NextDataRegex.Match(html);
        if (!match.Success) return [];

        using var doc = JsonDocument.Parse(match.Groups[1].Value);
        var root = doc.RootElement;

        if (!root.TryGetProperty("props", out var props) ||
            !props.TryGetProperty("pageProps", out var pageProps) ||
            !pageProps.TryGetProperty("response", out var responseArr) ||
            responseArr.ValueKind != JsonValueKind.Array ||
            responseArr.GetArrayLength() == 0)
            return [];

        var results = new List<ObsItem>();

        // Her istasyon için dataLast dizisini tara
        foreach (var station in responseArr.EnumerateArray())
        {
            if (!station.TryGetProperty("dataLast", out var dataLast) ||
                dataLast.ValueKind != JsonValueKind.Array)
                continue;

            foreach (var item in dataLast.EnumerateArray())
            {
                if (!item.TryGetProperty("observationType", out var typeProp)) continue;
                if (!item.TryGetProperty("observationText", out var textProp)) continue;

                var type = typeProp.GetInt32();
                var text = textProp.GetString()?.Trim();
                if (!string.IsNullOrWhiteSpace(text))
                    results.Add(new ObsItem(type, text));
            }
        }

        return results;
    }
}
