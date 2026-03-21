using System.Text.Json;
using MetarPulse.Abstractions.Providers;
using MetarPulse.Core.Models;
using MetarPulse.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MetarPulse.Infrastructure.Providers.Weather;

/// <summary>
/// AVWX REST API — https://avwx.rest
/// Ücretsiz plan: tek istasyon METAR/TAF. Geçmiş için ücretli plan gerekir.
/// </summary>
public class AvwxWeatherProvider : BaseWeatherProvider
{
    public override string ProviderName => "AVWX";

    public AvwxWeatherProvider(
        IHttpClientFactory httpClientFactory,
        IOptions<WeatherProviderSettings> settings,
        ILogger<AvwxWeatherProvider> logger)
        : base(
            httpClientFactory.CreateClient("AVWX"),
            settings.Value.Providers.GetValueOrDefault("AVWX") ?? new ProviderConfig(),
            logger)
    {
        var apiKey = Config.ApiKey;
        if (!string.IsNullOrEmpty(apiKey))
            _http.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", apiKey);
    }

    public override async Task<Metar?> GetMetarAsync(string icaoCode, CancellationToken ct = default)
    {
        var url = $"{Config.BaseUrl}/metar/{icaoCode.ToUpper()}?options=summary";
        var response = await GetWithResilienceAsync(url, ct);
        if (response == null || !response.IsSuccessStatusCode) return null;

        try
        {
            var json = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            var raw = doc.RootElement.GetProperty("raw").GetString();
            return raw != null ? MetarParser.Parse(raw, ProviderName) : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AVWX METAR parse hatası: {ICAO}", icaoCode);
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
            using var doc = JsonDocument.Parse(json);
            var raw = doc.RootElement.GetProperty("raw").GetString();
            return raw != null ? TafParser.Parse(raw, ProviderName) : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AVWX TAF parse hatası: {ICAO}", icaoCode);
            return null;
        }
    }

    public override async Task<List<Metar>> GetMetarHistoryAsync(
        string icaoCode, int hours = 24, CancellationToken ct = default)
    {
        // AVWX ücretsiz planda geçmiş yok; mevcut METAR'ı tek elemanlı liste olarak dön
        var metar = await GetMetarAsync(icaoCode, ct);
        return metar != null ? [metar] : [];
    }

    public override async Task<bool> HealthCheckAsync(CancellationToken ct = default)
    {
        try
        {
            var url = $"{Config.BaseUrl}/metar/LTBU?options=summary";
            var response = await GetWithResilienceAsync(url, ct);
            _logger.LogInformation("AVWX health check → status={Status} apiKeySet={HasKey}",
                response?.StatusCode.ToString() ?? "null",
                !string.IsNullOrEmpty(Config.ApiKey));
            if (response == null || !response.IsSuccessStatusCode) return false;
            var json = await response.Content.ReadAsStringAsync(ct);
            return json.Contains("\"raw\"");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AVWX health check exception");
            return false;
        }
    }
}
