using System.Text.Json;
using MetarPulse.Abstractions.Providers;
using MetarPulse.Core.Models;
using MetarPulse.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MetarPulse.Infrastructure.Providers.Weather;

/// <summary>
/// NOAA Aviation Weather Center — https://aviationweather.gov/api/data
/// API key gerektirmez. Ücretsiz, kamuya açık.
/// </summary>
public class AwcWeatherProvider : BaseWeatherProvider
{
    public override string ProviderName => "AWC";

    public AwcWeatherProvider(
        IHttpClientFactory httpClientFactory,
        IOptions<WeatherProviderSettings> settings,
        ILogger<AwcWeatherProvider> logger)
        : base(
            httpClientFactory.CreateClient("AWC"),
            settings.Value.Providers.GetValueOrDefault("AWC") ?? new ProviderConfig(),
            logger)
    {
    }

    public override async Task<Metar?> GetMetarAsync(string icaoCode, CancellationToken ct = default)
    {
        var url = $"{Config.BaseUrl}/metar?ids={icaoCode.ToUpper()}&format=json&hours=1";
        var response = await GetWithResilienceAsync(url, ct);
        if (response == null || !response.IsSuccessStatusCode) return null;

        try
        {
            var json = await response.Content.ReadAsStringAsync(ct);
            var raw = ExtractFirstRawOb(json);
            return raw != null ? MetarParser.Parse(raw, ProviderName) : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AWC METAR parse hatası: {ICAO}", icaoCode);
            return null;
        }
    }

    public override async Task<Taf?> GetTafAsync(string icaoCode, CancellationToken ct = default)
    {
        var url = $"{Config.BaseUrl}/taf?ids={icaoCode.ToUpper()}&format=json";
        var response = await GetWithResilienceAsync(url, ct);
        if (response == null || !response.IsSuccessStatusCode) return null;

        try
        {
            var json = await response.Content.ReadAsStringAsync(ct);
            var raw = ExtractFirstTafRaw(json);
            return raw != null ? TafParser.Parse(raw, ProviderName) : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AWC TAF parse hatası: {ICAO}", icaoCode);
            return null;
        }
    }

    public override async Task<List<Metar>> GetMetarHistoryAsync(
        string icaoCode, int hours = 24, CancellationToken ct = default)
    {
        var url = $"{Config.BaseUrl}/metar?ids={icaoCode.ToUpper()}&format=json&hours={hours}";
        var response = await GetWithResilienceAsync(url, ct);
        if (response == null || !response.IsSuccessStatusCode) return [];

        try
        {
            var json = await response.Content.ReadAsStringAsync(ct);
            return ExtractAllRawObs(json)
                .Select(raw => MetarParser.Parse(raw, ProviderName))
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AWC geçmiş parse hatası: {ICAO}", icaoCode);
            return [];
        }
    }

    // ── AWC JSON yapısı: [ { "rawOb": "METAR LTFM ..." }, ... ] ─────────────

    private static string? ExtractFirstRawOb(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Array) return null;
        foreach (var item in doc.RootElement.EnumerateArray())
            if (item.TryGetProperty("rawOb", out var raw) && raw.GetString() is { } text)
                return text;
        return null;
    }

    private static string? ExtractFirstTafRaw(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Array) return null;
        foreach (var item in doc.RootElement.EnumerateArray())
        {
            // AWC TAF: "rawTAF" veya "rawOb" field
            if (item.TryGetProperty("rawTAF", out var raw) && raw.GetString() is { } text)
                return text;
            if (item.TryGetProperty("rawOb", out var raw2) && raw2.GetString() is { } text2)
                return text2;
        }
        return null;
    }

    private static List<string> ExtractAllRawObs(string json)
    {
        var results = new List<string>();
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Array) return results;
        foreach (var item in doc.RootElement.EnumerateArray())
            if (item.TryGetProperty("rawOb", out var raw) && raw.GetString() is { } text)
                results.Add(text);
        return results;
    }
}
