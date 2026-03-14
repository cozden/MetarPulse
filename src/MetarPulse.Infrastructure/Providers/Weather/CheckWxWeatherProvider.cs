using System.Text.Json;
using MetarPulse.Abstractions.Providers;
using MetarPulse.Core.Models;
using MetarPulse.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MetarPulse.Infrastructure.Providers.Weather;

/// <summary>
/// CheckWX REST API — https://api.checkwx.com
/// X-API-Key header ile kimlik doğrulama.
/// </summary>
public class CheckWxWeatherProvider : BaseWeatherProvider
{
    public override string ProviderName => "CheckWX";

    public CheckWxWeatherProvider(
        IHttpClientFactory httpClientFactory,
        IOptions<WeatherProviderSettings> settings,
        ILogger<CheckWxWeatherProvider> logger)
        : base(
            httpClientFactory.CreateClient("CheckWX"),
            settings.Value.Providers.GetValueOrDefault("CheckWX") ?? new ProviderConfig(),
            logger)
    {
        var apiKey = Config.ApiKey;
        if (!string.IsNullOrEmpty(apiKey))
            _http.DefaultRequestHeaders.TryAddWithoutValidation("X-API-Key", apiKey);
    }

    public override async Task<Metar?> GetMetarAsync(string icaoCode, CancellationToken ct = default)
    {
        var url = $"{Config.BaseUrl}/metar/{icaoCode.ToUpper()}";
        var response = await GetWithResilienceAsync(url, ct);
        if (response == null || !response.IsSuccessStatusCode) return null;

        try
        {
            var json = await response.Content.ReadAsStringAsync(ct);
            var raw = ExtractFirstRawText(json, "raw_text");
            return raw != null ? MetarParser.Parse(raw, ProviderName) : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CheckWX METAR parse hatası: {ICAO}", icaoCode);
            return null;
        }
    }

    public override async Task<Taf?> GetTafAsync(string icaoCode, CancellationToken ct = default)
    {
        var url = $"{Config.BaseUrl}/taf/{icaoCode.ToUpper()}";
        var response = await GetWithResilienceAsync(url, ct);
        if (response == null || !response.IsSuccessStatusCode) return null;

        try
        {
            var json = await response.Content.ReadAsStringAsync(ct);
            var raw = ExtractFirstRawText(json, "raw_text");
            return raw != null ? TafParser.Parse(raw, ProviderName) : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CheckWX TAF parse hatası: {ICAO}", icaoCode);
            return null;
        }
    }

    public override async Task<List<Metar>> GetMetarHistoryAsync(
        string icaoCode, int hours = 24, CancellationToken ct = default)
    {
        // CheckWX: /metar/{icao}/{count} ile birden fazla METAR
        var count = Math.Max(1, Math.Min(hours, 48));
        var url = $"{Config.BaseUrl}/metar/{icaoCode.ToUpper()}/{count}";
        var response = await GetWithResilienceAsync(url, ct);
        if (response == null || !response.IsSuccessStatusCode) return [];

        try
        {
            var json = await response.Content.ReadAsStringAsync(ct);
            return ExtractRawTexts(json, "raw_text")
                .Select(raw => MetarParser.Parse(raw, ProviderName))
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CheckWX geçmiş parse hatası: {ICAO}", icaoCode);
            return [];
        }
    }

    // ── CheckWX JSON yapısı: { "results": 1, "data": [ { "raw_text": "..." } ] }

    private static string? ExtractFirstRawText(string json, string fieldName)
    {
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("data", out var data)) return null;
        if (data.ValueKind != JsonValueKind.Array || data.GetArrayLength() == 0) return null;
        var first = data[0];
        return first.TryGetProperty(fieldName, out var raw) ? raw.GetString() : null;
    }

    private static List<string> ExtractRawTexts(string json, string fieldName)
    {
        var results = new List<string>();
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("data", out var data)) return results;
        if (data.ValueKind != JsonValueKind.Array) return results;
        foreach (var item in data.EnumerateArray())
            if (item.TryGetProperty(fieldName, out var raw) && raw.GetString() is { } text)
                results.Add(text);
        return results;
    }
}
