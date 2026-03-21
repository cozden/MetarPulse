using MetarPulse.Infrastructure.Persistence.PostgreSQL;
using Microsoft.EntityFrameworkCore;

namespace MetarPulse.Api.Services;

/// <summary>
/// Günlük arka plan servisi — METAR ve TAF geçmişini temizler.
/// 2 günden eski kayıtlar silinir; kullanıcı verileri dokunulmaz.
/// Her gün 03:00 UTC'de çalışır.
/// </summary>
public class WeatherDataCleanupService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<WeatherDataCleanupService> _logger;

    private const int RetentionDays = 2;

    public WeatherDataCleanupService(
        IServiceScopeFactory scopeFactory,
        ILogger<WeatherDataCleanupService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "WeatherDataCleanupService başlatıldı (saklama süresi: {Days} gün, çalışma saati: 03:00 UTC).",
            RetentionDays);

        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = TimeUntilNextRun();
            _logger.LogDebug("Sonraki temizlik: {Next:yyyy-MM-dd HH:mm} UTC ({Hours:F1}s sonra).",
                DateTime.UtcNow.Add(delay), delay.TotalHours);

            try { await Task.Delay(delay, stoppingToken); }
            catch (OperationCanceledException) { break; }

            if (!stoppingToken.IsCancellationRequested)
                await RunCleanupAsync(stoppingToken);
        }
    }

    private async Task RunCleanupAsync(CancellationToken ct)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var cutoff = DateTime.UtcNow.AddDays(-RetentionDays);

            var metarDeleted = await db.MetarHistory
                .Where(m => m.FetchedAt < cutoff)
                .ExecuteDeleteAsync(ct);

            var tafDeleted = await db.TafHistory
                .Where(t => t.FetchedAt < cutoff)
                .ExecuteDeleteAsync(ct);

            _logger.LogInformation(
                "Temizlik tamamlandı — {Cutoff:yyyy-MM-dd} öncesi: METAR {M} kayıt, TAF {T} kayıt silindi.",
                cutoff, metarDeleted, tafDeleted);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "WeatherDataCleanupService hata.");
        }
    }

    /// <summary>Bir sonraki 03:00 UTC'ye kadar olan bekleme süresi.</summary>
    private static TimeSpan TimeUntilNextRun()
    {
        var now  = DateTime.UtcNow;
        var next = now.Date.AddHours(3); // bugün 03:00 UTC
        if (next <= now)
            next = next.AddDays(1);     // geçtiyse yarın 03:00
        return next - now;
    }
}
