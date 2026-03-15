using MetarPulse.Abstractions.Providers;
using MetarPulse.Core.Models;
using MetarPulse.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MetarPulse.Infrastructure.Providers.Weather;

/// <summary>
/// NOAA TGFTP (Telecommunications Gateway) — ham METAR/TAF metin dosyaları.
/// AWC JSON API'den bağımsız, doğrudan NWS FTP mirror üzerinden çalışır.
/// METAR: https://tgftp.nws.noaa.gov/data/observations/metar/stations/{ICAO}.TXT
/// TAF  : https://tgftp.nws.noaa.gov/data/forecasts/taf/stations/{ICAO}.TXT
/// </summary>
public class NoaaTgftpWeatherProvider : BaseWeatherProvider
{
    public override string ProviderName => "NOAA_TGFTP";

    public NoaaTgftpWeatherProvider(
        IHttpClientFactory httpClientFactory,
        IOptions<WeatherProviderSettings> settings,
        ILogger<NoaaTgftpWeatherProvider> logger)
        : base(
            httpClientFactory.CreateClient("NOAA_TGFTP"),
            settings.Value.Providers.GetValueOrDefault("NOAA_TGFTP") ?? new ProviderConfig(),
            logger)
    {
    }

    public override async Task<Metar?> GetMetarAsync(string icaoCode, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(icaoCode)) return null;

        var url = $"{Config.BaseUrl}/data/observations/metar/stations/{icaoCode.ToUpper()}.TXT";
        _logger.LogDebug("NOAA_TGFTP METAR isteği: {Url}", url);

        var response = await GetWithResilienceAsync(url, ct);
        if (response == null || !response.IsSuccessStatusCode) return null;

        try
        {
            var text = await response.Content.ReadAsStringAsync(ct);
            var raw  = ExtractMetarLine(text);
            return raw != null ? MetarParser.Parse(raw, ProviderName) : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "NOAA_TGFTP METAR parse hatası: {ICAO}", icaoCode);
            return null;
        }
    }

    public override async Task<Taf?> GetTafAsync(string icaoCode, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(icaoCode)) return null;

        var url = $"{Config.BaseUrl}/data/forecasts/taf/stations/{icaoCode.ToUpper()}.TXT";
        _logger.LogDebug("NOAA_TGFTP TAF isteği: {Url}", url);

        var response = await GetWithResilienceAsync(url, ct);
        if (response == null || !response.IsSuccessStatusCode) return null;

        try
        {
            var text = await response.Content.ReadAsStringAsync(ct);
            var raw  = ExtractTafText(text);
            return raw != null ? TafParser.Parse(raw, ProviderName) : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "NOAA_TGFTP TAF parse hatası: {ICAO}", icaoCode);
            return null;
        }
    }

    public override async Task<List<Metar>> GetMetarHistoryAsync(
        string icaoCode, int hours = 24, CancellationToken ct = default)
    {
        // TGFTP yalnızca güncel gözlemi döner; history için tek kayıt dön
        var current = await GetMetarAsync(icaoCode, ct);
        return current != null ? [current] : [];
    }

    // ── Format: ilk satır "yyyy/MM/dd HH:mm", ikinci satır ham METAR ─────────

    /// <summary>
    /// Dosya içeriğinden ham METAR satırını çıkarır.
    /// Format: "2024/01/15 18:55\nKORD 151855Z ..."
    /// </summary>
    private static string? ExtractMetarLine(string text)
    {
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        // İlk satır tarih damgası, ikinci satır METAR
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("METAR", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("SPECI", StringComparison.OrdinalIgnoreCase) ||
                (trimmed.Length >= 4 && char.IsLetter(trimmed[0]) &&
                 !trimmed.Contains('/') && trimmed.Contains('Z')))
                return trimmed;
        }
        return null;
    }

    /// <summary>
    /// TAF dosyasından ham TAF metnini çıkarır (çok satırlı olabilir).
    /// </summary>
    private static string? ExtractTafText(string text)
    {
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var tafLines = new List<string>();
        bool inTaf = false;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("TAF", StringComparison.OrdinalIgnoreCase))
            {
                inTaf = true;
                tafLines.Add(trimmed);
            }
            else if (inTaf && !string.IsNullOrWhiteSpace(trimmed))
            {
                tafLines.Add(trimmed);
            }
        }

        return tafLines.Count > 0 ? string.Join(" ", tafLines) : null;
    }
}
