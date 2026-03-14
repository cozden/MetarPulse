using MetarPulse.Abstractions.Providers;
using MetarPulse.Core.Models;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Timeout;

namespace MetarPulse.Infrastructure.Providers.Weather;

/// <summary>
/// Tüm METAR/TAF provider'larının ortak altyapısı.
/// Polly resilience pipeline (retry + circuit breaker + timeout) burada kurulur.
/// </summary>
public abstract class BaseWeatherProvider : IWeatherProvider
{
    protected readonly HttpClient _http;
    protected readonly ILogger _logger;
    private readonly ResiliencePipeline<HttpResponseMessage> _pipeline;

    protected ProviderConfig Config { get; }

    public abstract string ProviderName { get; }
    public int Priority => Config.Priority;
    public bool IsEnabled => Config.Enabled;

    protected BaseWeatherProvider(
        HttpClient http,
        ProviderConfig config,
        ILogger logger)
    {
        _http = http;
        Config = config;
        _logger = logger;
        _pipeline = BuildPipeline(config);
    }

    // ── IWeatherProvider ─────────────────────────────────────────────────────

    public abstract Task<Metar?> GetMetarAsync(string icaoCode, CancellationToken ct = default);
    public abstract Task<Taf?> GetTafAsync(string icaoCode, CancellationToken ct = default);
    public abstract Task<List<Metar>> GetMetarHistoryAsync(string icaoCode, int hours = 24, CancellationToken ct = default);

    public virtual async Task<bool> HealthCheckAsync(CancellationToken ct = default)
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

    // ── Yardımcı metodlar ────────────────────────────────────────────────────

    /// <summary>Resilience pipeline ile HTTP GET çağrısı yapar.</summary>
    protected async Task<HttpResponseMessage?> GetWithResilienceAsync(string url, CancellationToken ct)
    {
        try
        {
            return await _pipeline.ExecuteAsync(
                async token => await _http.GetAsync(url, token), ct);
        }
        catch (BrokenCircuitException)
        {
            _logger.LogWarning("{Provider} circuit breaker açık, istek atlandı.", ProviderName);
            return null;
        }
        catch (TimeoutRejectedException)
        {
            _logger.LogWarning("{Provider} zaman aşımı: {Url}", ProviderName, url);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Provider} HTTP hatası: {Url}", ProviderName, url);
            return null;
        }
    }

    // ── Polly pipeline kurulumu ──────────────────────────────────────────────

    private static ResiliencePipeline<HttpResponseMessage> BuildPipeline(ProviderConfig cfg)
    {
        return new ResiliencePipelineBuilder<HttpResponseMessage>()
            .AddTimeout(new TimeoutStrategyOptions
            {
                Timeout = TimeSpan.FromSeconds(cfg.TimeoutSeconds > 0 ? cfg.TimeoutSeconds : 5)
            })
            .AddRetry(new RetryStrategyOptions<HttpResponseMessage>
            {
                MaxRetryAttempts = cfg.RetryCount > 0 ? cfg.RetryCount : 2,
                BackoffType = DelayBackoffType.Exponential,
                Delay = TimeSpan.FromMilliseconds(300),
                ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                    .Handle<HttpRequestException>()
                    .HandleResult(r => (int)r.StatusCode >= 500)
            })
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions<HttpResponseMessage>
            {
                FailureRatio = 0.5,
                MinimumThroughput = cfg.CircuitBreakerThreshold > 0 ? cfg.CircuitBreakerThreshold : 5,
                BreakDuration = TimeSpan.FromSeconds(
                    cfg.CircuitBreakerDurationSeconds > 0 ? cfg.CircuitBreakerDurationSeconds : 30),
                ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                    .Handle<HttpRequestException>()
                    .HandleResult(r => (int)r.StatusCode >= 500)
            })
            .Build();
    }
}
