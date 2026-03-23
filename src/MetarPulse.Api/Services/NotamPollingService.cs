using MetarPulse.Abstractions.Providers;
using MetarPulse.Api.Hubs;
using MetarPulse.Infrastructure.Providers.Notam;
using MetarPulse.Core.Enums;
using MetarPulse.Infrastructure.Persistence.PostgreSQL;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace MetarPulse.Api.Services;

/// <summary>
/// Arka plan servisi — takip edilen meydanlar için NOTAM'ları 30 dk'da bir günceller.
/// Yeni/değişen NOTAM varsa SignalR ile push yapar.
/// </summary>
public class NotamPollingService : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(30);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHubContext<MetarHub> _hub;
    private readonly IMemoryCache _cache;
    private readonly FcmService _fcm;
    private readonly ILogger<NotamPollingService> _logger;

    public NotamPollingService(
        IServiceScopeFactory scopeFactory,
        IHubContext<MetarHub> hub,
        IMemoryCache cache,
        FcmService fcm,
        ILogger<NotamPollingService> logger)
    {
        _scopeFactory = scopeFactory;
        _hub = hub;
        _cache = cache;
        _fcm = fcm;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("NotamPollingService başlatıldı (aralık: {Min} dk).", PollInterval.TotalMinutes);

        try
        {
            // İlk çalıştırma — 2 dk bekle (API başlayana kadar)
            await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                await PollAsync(stoppingToken);
                await Task.Delay(PollInterval, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown, ignore
        }
    }

    private async Task PollAsync(CancellationToken ct)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var provider = scope.ServiceProvider.GetRequiredService<INotamAggregator>();

            // Tüm bookmark'lanan benzersiz ICAO'lar
            var stations = await db.UserBookmarks
                .Select(b => b.StationIcao)
                .Distinct()
                .ToListAsync(ct);

            if (stations.Count == 0) return;

            _logger.LogDebug("NOTAM polling: {Count} meydan kontrol ediliyor.", stations.Count);
            var updated = 0;

            foreach (var icao in stations)
            {
                if (ct.IsCancellationRequested) break;

                try
                {
                    await UpdateNotamsForStationAsync(db, provider, icao, ct);
                    updated++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "NOTAM polling hatası: {ICAO}", icao);
                }
            }

            _logger.LogInformation("NOTAM polling tamamlandı: {Updated}/{Total} meydan güncellendi.",
                updated, stations.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "NotamPollingService genel hata.");
        }
    }

    private async Task UpdateNotamsForStationAsync(
        AppDbContext db,
        INotamAggregator provider,
        string icao,
        CancellationToken ct)
    {
        var fresh = await provider.GetNotamsAsync(icao, ct);
        if (fresh.Count == 0) return;

        // Mevcut aktif NOTAM ID listesi (karşılaştırma için)
        var previousIds = await db.Notams
            .Where(n => n.AirportIdent == icao)
            .Select(n => n.NotamId)
            .ToHashSetAsync(ct);

        var newIds = fresh.Select(n => n.NotamId).ToHashSet();
        var hasChanges = !previousIds.SetEquals(newIds);

        if (!hasChanges) return;

        // Eski NOTAM'ları sil, yenilerini ekle
        var existing = await db.Notams.Where(n => n.AirportIdent == icao).ToListAsync(ct);
        db.Notams.RemoveRange(existing);
        await db.Notams.AddRangeAsync(fresh, ct);
        await db.SaveChangesAsync(ct);

        // Cache'i temizle — sonraki istekte taze veri gelsin
        _cache.Remove($"notam:{icao}");

        // SignalR push
        var activeNotams = fresh.Where(n => n.IsActive).ToList();
        var hasOperationsCritical = activeNotams.Any(n => n.VfrImpact == NotamVfrImpact.OperationsCritical);
        var hasWarning = activeNotams.Any(n => n.VfrImpact == NotamVfrImpact.Warning);
        var hasCaution = activeNotams.Any(n => n.VfrImpact == NotamVfrImpact.Caution);

        await _hub.Clients
            .Group(MetarHub.StationGroup(icao))
            .SendAsync("NotamUpdate", new
            {
                stationId = icao,
                count = activeNotams.Count,
                hasOperationsCritical,
                hasVfrWarning = hasWarning,
                hasVfrCaution = hasCaution
            }, ct);

        // OPS KISITMASI tespitinde push bildirim gönder
        if (hasOperationsCritical)
        {
            var criticalCount = activeNotams.Count(n => n.VfrImpact == NotamVfrImpact.OperationsCritical);
            _logger.LogWarning(
                "OPS KISITMASI tespit edildi: {ICAO} — {Count} kritik NOTAM. Push bildirim gönderiliyor.",
                icao, criticalCount);

            await SendOperationsCriticalPushAsync(icao, criticalCount, ct);
        }

        _logger.LogInformation(
            "NOTAM güncellendi: {ICAO} — {Count} aktif NOTAM (OpsKisit:{OC}, Warning:{W}, Caution:{C})",
            icao, activeNotams.Count, hasOperationsCritical, hasWarning, hasCaution);
    }

    /// <summary>
    /// OPS KISITMASI tespit edildiğinde bu meydanı takip eden tüm kullanıcılara
    /// SignalR + FCM push bildirimi gönderir.
    /// </summary>
    private async Task SendOperationsCriticalPushAsync(string icao, int criticalCount, CancellationToken ct)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            // Bu meydanı bookmark'lamış tüm kullanıcı ID'leri
            var userIds = await db.UserBookmarks
                .Where(b => b.StationIcao == icao)
                .Select(b => b.UserId)
                .Distinct()
                .ToListAsync(ct);

            if (userIds.Count == 0) return;

            var title = $"⚠ OPS KISITMASI — {icao}";
            var body  = criticalCount == 1
                ? $"{icao} meydanında uçuş operasyonlarını kısıtlayan 1 NOTAM yayınlandı."
                : $"{icao} meydanında uçuş operasyonlarını kısıtlayan {criticalCount} NOTAM yayınlandı.";

            var data = new Dictionary<string, string>
            {
                ["icao"]      = icao,
                ["alertType"] = "OperationsCritical",
                ["count"]     = criticalCount.ToString()
            };

            var alert = new
            {
                stationId = icao,
                alertType = "OperationsCritical",
                title,
                body
            };

            foreach (var userId in userIds)
            {
                // SignalR — uygulama açıksa anlık bildirim
                await _hub.Clients
                    .Group(MetarHub.UserGroup(userId))
                    .SendAsync("ReceiveAlert", alert, ct);

                // FCM — uygulama kapalıysa push
                var tokens = await db.DeviceTokens
                    .Where(d => d.UserId == userId)
                    .Select(d => d.Token)
                    .ToListAsync(ct);

                if (tokens.Count == 0) continue;

                var invalidTokens = await _fcm.SendMulticastAsync(tokens, title, body, data, ct);

                if (invalidTokens.Count > 0)
                {
                    var toRemove = await db.DeviceTokens
                        .Where(d => invalidTokens.Contains(d.Token))
                        .ToListAsync(ct);
                    db.DeviceTokens.RemoveRange(toRemove);
                    await db.SaveChangesAsync(ct);
                }
            }

            _logger.LogInformation(
                "OPS KISITMASI push gönderildi: {ICAO} → {UserCount} kullanıcı",
                icao, userIds.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OPS KISITMASI push hatası: {ICAO}", icao);
        }
    }
}
