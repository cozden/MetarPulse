using System.Text.Json;
using MetarPulse.Abstractions.Providers;
using MetarPulse.Core.Models;
using MetarPulse.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MetarPulse.Infrastructure.Providers.Weather;

/// <summary>
/// MGM Rasat METAR servisi — Türkiye meydanları için birincil kaynak.
/// LT* prefix'li ICAO kodları için öncelikli olarak kullanılır (~0-1dk gecikme).
/// Endpoint: GET {BaseUrl}/api/aviation/metar/{ICAO}
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

        var url = $"{Config.BaseUrl}/api/aviation/metar/{icaoCode.ToUpper()}";
        var response = await GetWithResilienceAsync(url, ct);
        if (response == null || !response.IsSuccessStatusCode) return null;

        try
        {
            var json = await response.Content.ReadAsStringAsync(ct);
            var raw = ExtractRawMetar(json, icaoCode);
            return raw != null ? MetarParser.Parse(raw, ProviderName) : null;
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

        var url = $"{Config.BaseUrl}/api/aviation/taf/{icaoCode.ToUpper()}";
        var response = await GetWithResilienceAsync(url, ct);
        if (response == null || !response.IsSuccessStatusCode) return null;

        try
        {
            var json = await response.Content.ReadAsStringAsync(ct);
            var raw = ExtractRawTaf(json, icaoCode);
            return raw != null ? TafParser.Parse(raw, ProviderName) : null;
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

        var url = $"{Config.BaseUrl}/api/aviation/metar/{icaoCode.ToUpper()}/history?hours={hours}";
        var response = await GetWithResilienceAsync(url, ct);
        if (response == null || !response.IsSuccessStatusCode)
        {
            // Geçmiş endpoint yoksa mevcut METAR'ı dön
            var current = await GetMetarAsync(icaoCode, ct);
            return current != null ? [current] : [];
        }

        try
        {
            var json = await response.Content.ReadAsStringAsync(ct);
            return ExtractRawMetarList(json)
                .Select(raw => MetarParser.Parse(raw, ProviderName))
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MGM_RASAT geçmiş parse hatası: {ICAO}", icaoCode);
            return [];
        }
    }

    public override async Task<bool> HealthCheckAsync(CancellationToken ct = default)
    {
        try
        {
            // Önce LTFM ile gerçek veri dene
            var metar = await GetMetarAsync("LTFM", ct);
            if (metar != null) return true;

            // METAR null geldiyse endpoint'in erişilebilir olup olmadığını kontrol et
            var response = await GetWithResilienceAsync($"{Config.BaseUrl}/api/aviation/metar/LTFM", ct);
            // 200, 404, 400 → sunucu yanıt veriyor demektir; null → bağlantı yok
            return response != null;
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
    /// MGM API yanıtından ham METAR metnini çıkarır.
    /// Beklenen format: { "metar": "METAR LTFM ..." } veya düz metin
    /// </summary>
    private static string? ExtractRawMetar(string json, string icaoCode)
    {
        // Düz metin yanıt (ham METAR içeriyorsa)
        var trimmed = json.Trim().Trim('"');
        if (trimmed.StartsWith("METAR", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("SPECI", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith(icaoCode.ToUpper(), StringComparison.OrdinalIgnoreCase))
            return trimmed;

        // JSON yapısı dene
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // { "metar": "..." }
            if (root.TryGetProperty("metar", out var m) && m.GetString() is { } s1) return s1;

            // { "raw": "..." }
            if (root.TryGetProperty("raw", out var r) && r.GetString() is { } s2) return s2;

            // { "data": { "metar": "..." } }
            if (root.TryGetProperty("data", out var d))
            {
                if (d.TryGetProperty("metar", out var dm) && dm.GetString() is { } s3) return s3;
                if (d.TryGetProperty("raw", out var dr) && dr.GetString() is { } s4) return s4;
            }

            // Array: [ { "metar": "..." }, ... ]
            if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0)
            {
                var first = root[0];
                if (first.TryGetProperty("metar", out var am) && am.GetString() is { } s5) return s5;
                if (first.TryGetProperty("raw", out var ar) && ar.GetString() is { } s6) return s6;
            }
        }
        catch { /* JSON değil, düz metin olabilir */ }

        return null;
    }

    private static string? ExtractRawTaf(string json, string icaoCode)
    {
        var trimmed = json.Trim().Trim('"');
        if (trimmed.StartsWith("TAF", StringComparison.OrdinalIgnoreCase)) return trimmed;

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.TryGetProperty("taf", out var t) && t.GetString() is { } s1) return s1;
            if (root.TryGetProperty("raw", out var r) && r.GetString() is { } s2) return s2;
            if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0)
            {
                var first = root[0];
                if (first.TryGetProperty("taf", out var at) && at.GetString() is { } s3) return s3;
                if (first.TryGetProperty("raw", out var ar) && ar.GetString() is { } s4) return s4;
            }
        }
        catch { }

        return null;
    }

    private static List<string> ExtractRawMetarList(string json)
    {
        var results = new List<string>();
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var array = root.ValueKind == JsonValueKind.Array ? root
                : root.TryGetProperty("data", out var d) ? d : default;

            if (array.ValueKind == JsonValueKind.Array)
                foreach (var item in array.EnumerateArray())
                {
                    if (item.TryGetProperty("metar", out var m) && m.GetString() is { } s) results.Add(s);
                    else if (item.TryGetProperty("raw", out var r) && r.GetString() is { } s2) results.Add(s2);
                }
        }
        catch { }
        return results;
    }
}
