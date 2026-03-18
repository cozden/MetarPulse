using MetarPulse.Abstractions.Providers;
using MetarPulse.Abstractions.Repositories;
using MetarPulse.Api.Hubs;
using MetarPulse.Core.Enums;
using MetarPulse.Core.Models;
using MetarPulse.Core.Services;
using MetarPulse.Infrastructure.Persistence.PostgreSQL;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace MetarPulse.Api.Services;

/// <summary>
/// Arka plan servisi — favori istasyonlar için METAR'ı periyodik günceller.
///
/// Akıllı zamanlama:
///   • METAR yayın penceresinde (XX:20 ve XX:50): her 15 sn — yeni METAR gelene kadar
///   • Yeni METAR alındıktan sonra (ObservationTime değişti): normal aralığa (5 dk) dön
///   • METAR gecikmeli gelse bile (örn. :56'da) yeni gelince polling yavaşlar
///   • Normal zamanlarda: her 5 dk
///
/// Gerçek yeni ObservationTime geldiğinde SignalR push ve bildirim tetiklenir.
/// </summary>
public class MetarPollingService : BackgroundService
{
    /// <summary>Servisin uyku döngüsü — her zaman 15sn.</summary>
    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(15);

    /// <summary>METAR yayın penceresi dışında minimum provider aralığı.</summary>
    private static readonly TimeSpan IdleMinProviderInterval = TimeSpan.FromMinutes(5);

    /// <summary>METAR yayın penceresi içinde minimum provider aralığı.</summary>
    private static readonly TimeSpan HotMinProviderInterval = TimeSpan.FromSeconds(15);

    /// <summary>
    /// XX:20 ve XX:50'den sonraki "sıcak pencere" süresi (dakika).
    /// Maksimum bekleme süresi — yeni METAR gelince pencere erken kapanır.
    /// </summary>
    private const int HotWindowMinutes = 15;

    /// <summary>İstasyon bazlı son provider çağrı zamanları (UTC).</summary>
    private readonly Dictionary<string, DateTime> _lastFetched = new();

    /// <summary>
    /// İstasyon bazlı: bu döngüde (XX:20 veya XX:50) yeni METAR alındı mı?
    /// Key: ICAO, Value: yeni METAR'ın alındığı UTC zaman.
    /// Döngü başlangıcından (cycleStart) sonra değer varsa → normal aralığa dön.
    /// </summary>
    private readonly Dictionary<string, DateTime> _newMetarReceivedAt = new();

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHubContext<MetarHub> _hub;
    private readonly FcmService _fcm;
    private readonly ILogger<MetarPollingService> _logger;

    public MetarPollingService(
        IServiceScopeFactory scopeFactory,
        IHubContext<MetarHub> hub,
        FcmService fcm,
        ILogger<MetarPollingService> logger)
    {
        _scopeFactory = scopeFactory;
        _hub = hub;
        _fcm = fcm;
        _logger = logger;
    }

    /// <summary>
    /// Şu an UTC dakikasının METAR yayın penceresinde olup olmadığını döner.
    /// Pencereler: XX:20–XX:34 ve XX:50–XX+1:04
    /// Not: XX:50 penceresi saat sınırını geçer (dakika 50-59 + 0-4).
    /// </summary>
    private static bool IsInHotWindow()
    {
        var minute = DateTime.UtcNow.Minute;
        return (minute >= 20 && minute < 35)   // XX:20–XX:34
            || (minute >= 50)                  // XX:50–XX:59
            || (minute < 5);                   // XX+1:00–XX+1:04
    }

    /// <summary>
    /// Mevcut döngünün başlangıç zamanını döner (XX:20 veya XX:50 UTC).
    /// Hot window dışındaysa DateTime.MinValue döner.
    /// </summary>
    private static DateTime GetCurrentCycleStart()
    {
        var now    = DateTime.UtcNow;
        var minute = now.Minute;

        if (minute >= 20 && minute < 35)
            return new DateTime(now.Year, now.Month, now.Day, now.Hour, 20, 0, DateTimeKind.Utc);

        if (minute >= 50)
            return new DateTime(now.Year, now.Month, now.Day, now.Hour, 50, 0, DateTimeKind.Utc);

        if (minute < 5)
        {
            // Saat sınırı geçildi: döngü önceki saatin :50'sinde başladı
            var prev = now.AddHours(-1);
            return new DateTime(prev.Year, prev.Month, prev.Day, prev.Hour, 50, 0, DateTimeKind.Utc);
        }

        return DateTime.MinValue;
    }

    /// <summary>
    /// İstasyon için efektif minimum aralığı belirler:
    /// • Hot window içinde ve bu döngüde yeni METAR henüz gelmemişse → 15 sn
    /// • Hot window içinde ama yeni METAR zaten geldiyse → 5 dk (bitti, dinlen)
    /// • Hot window dışında → 5 dk
    /// </summary>
    private TimeSpan GetEffectiveInterval(string icao)
    {
        if (!IsInHotWindow())
            return IdleMinProviderInterval;

        var cycleStart = GetCurrentCycleStart();

        // Bu döngüde yeni METAR alındı mı?
        if (_newMetarReceivedAt.TryGetValue(icao, out var receivedAt)
            && receivedAt >= cycleStart)
        {
            return IdleMinProviderInterval; // Yeni METAR geldi, normal aralığa dön
        }

        return HotMinProviderInterval; // Henüz yeni METAR yok, sık kontrol et
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "MetarPollingService başlatıldı (döngü: {S}sn, sıcak pencere: :20+{W}dk / :50+{W}dk).",
            CheckInterval.TotalSeconds, HotWindowMinutes, HotWindowMinutes);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(CheckInterval, stoppingToken);

                if (!stoppingToken.IsCancellationRequested)
                    await PollAllTrackedStationsAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown, ignore
        }
    }

    private async Task PollAllTrackedStationsAsync(CancellationToken ct)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db       = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var provider = scope.ServiceProvider.GetRequiredService<IProviderManager>();
            var metarRepo = scope.ServiceProvider.GetRequiredService<IMetarRepository>();

            // Bookmark'lanmış tüm benzersiz ICAO kodlarını al
            var stations = await db.Set<UserBookmark>()
                .Select(b => b.StationIcao)
                .Distinct()
                .ToListAsync(ct);

            if (stations.Count == 0)
            {
                _logger.LogDebug("Takip edilen istasyon yok, polling atlanıyor.");
                return;
            }

            _logger.LogDebug("Polling döngüsü: {Count} takip edilen istasyon kontrol ediliyor.", stations.Count);
            var updated = 0;

            foreach (var icao in stations)
            {
                if (ct.IsCancellationRequested) break;

                try
                {
                    // İstasyona özgü efektif aralık: hot window + yeni METAR durumuna göre
                    var minInterval = GetEffectiveInterval(icao);

                    var now = DateTime.UtcNow;
                    if (_lastFetched.TryGetValue(icao, out var lastFetch)
                        && now - lastFetch < minInterval)
                    {
                        _logger.LogDebug("{ICAO} atlandı — son çekim {Ago:F0}sn önce, aralık {Interval}.",
                            icao, (now - lastFetch).TotalSeconds, minInterval);
                        continue;
                    }

                    var previous = await metarRepo.GetLatestAsync(icao, ct);
                    var fresh    = await provider.GetMetarWithFallbackAsync(icao, ct);
                    _lastFetched[icao] = DateTime.UtcNow;

                    if (fresh == null) continue;

                    // Gerçekten yeni bir METAR mı? ObservationTime değişmediyse kaydetme/push etme
                    if (previous != null
                        && fresh.ObservationTime <= previous.ObservationTime)
                    {
                        _logger.LogDebug("{ICAO} aynı ObservationTime ({ObsTime:HH:mm}Z) — provider henüz güncellememiş.",
                            icao, fresh.ObservationTime);
                        continue;
                    }

                    // DB'ye kaydet
                    await metarRepo.AddAsync(fresh, ct);
                    await db.SaveChangesAsync(ct);

                    // Bu döngüde yeni METAR alındı — bir sonraki kontrolde normal aralığa geç
                    _newMetarReceivedAt[icao] = DateTime.UtcNow;

                    // Değişiklik analizi
                    MetarComparison? diff = null;
                    if (previous != null)
                    {
                        diff = MetarDiffEngine.Compare(previous, fresh);
                        if (diff.CategoryChanged || diff.SignificantWeatherChanged)
                            _logger.LogInformation(
                                "{ICAO} uçuş koşulları değişti: {Summary}",
                                icao, string.Join(", ", diff.ChangeSummary));
                    }

                    // SignalR push — istasyon grubuna
                    var payload = BuildMetarPayload(fresh);
                    await _hub.Clients
                        .Group(MetarHub.StationGroup(icao))
                        .SendAsync("ReceiveMetar", payload, ct);

                    // Bildirim kontrolü — bu istasyonu takip eden kullanıcılar
                    await TriggerNotificationsAsync(db, icao, fresh, diff, ct);

                    updated++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Polling hatası: {ICAO}", icao);
                }
            }

            _logger.LogInformation("Polling tamamlandı: {Updated}/{Total} istasyon güncellendi.",
                updated, stations.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MetarPollingService genel hata.");
        }
    }

    /// <summary>
    /// NotificationPreference kayıtlarını kontrol et — uygun kullanıcılara
    /// SignalR "ReceiveAlert" ve FCM push bildirimi gönder.
    /// </summary>
    private async Task TriggerNotificationsAsync(
        AppDbContext db,
        string icao,
        Metar fresh,
        MetarComparison? diff,
        CancellationToken ct)
    {
        // Bu istasyona ait tüm kullanıcı tercihleri
        var prefs = await db.NotificationPreferences
            .Where(p => p.StationIcao == icao)
            .ToListAsync(ct);

        if (prefs.Count == 0) return;

        foreach (var pref in prefs)
        {
            // Gün/saat filtresi — kullanıcının saat dilimine göre kontrol et
            if (!IsWithinActiveSchedule(pref)) continue;

            var reasons = BuildAlertReasons(pref, fresh, diff);
            if (reasons.Count == 0) continue;

            var alert = new
            {
                StationId = icao,
                Category  = fresh.Category.ToString(),
                Reasons   = reasons,
                ObservationTime = fresh.ObservationTime.ToString("O")
            };

            // 1) SignalR push — uygulama açıkken anlık güncelleme
            await _hub.Clients
                .Group(MetarHub.UserGroup(pref.UserId))
                .SendAsync("ReceiveAlert", alert, ct);

            // 2) FCM push — uygulama kapalıyken arka plan bildirimi
            var tokens = await db.DeviceTokens
                .Where(d => d.UserId == pref.UserId)
                .Select(d => d.Token)
                .ToListAsync(ct);

            if (tokens.Count > 0)
            {
                var title = $"{icao} — {fresh.Category}";
                var body  = string.Join(" | ", reasons);

                // MVFR/IFR/LIFR durumunda görüş ve tavan bilgisi ekle
                if (fresh.Category != FlightCategory.VFR)
                {
                    var extras = new List<string>();
                    if (fresh.VisibilityMeters > 0)
                        extras.Add($"VIS {fresh.VisibilityMeters}m");
                    if (fresh.CeilingFeet > 0)
                        extras.Add($"Tavan {fresh.CeilingFeet}ft");
                    if (extras.Count > 0)
                        body = string.IsNullOrEmpty(body)
                            ? string.Join(" | ", extras)
                            : $"{body} | {string.Join(" | ", extras)}";
                }

                var data  = new Dictionary<string, string>
                {
                    ["icao"]     = icao,
                    ["category"] = fresh.Category.ToString(),
                    ["obsTime"]  = fresh.ObservationTime.ToString("O")
                };

                var invalidTokens = await _fcm.SendMulticastAsync(tokens, title, body, data, ct);

                // Geçersiz token'ları DB'den temizle
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
                "Bildirim gönderildi → UserId:{UserId} | {ICAO} | {Reasons} | FCM:{FcmCount}",
                pref.UserId, icao, string.Join(", ", reasons), tokens.Count);
        }
    }

    private static bool IsWithinActiveSchedule(NotificationPreference pref)
    {
        try
        {
            var tz  = TimeZoneInfo.FindSystemTimeZoneById(pref.TimeZoneId);
            var now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);

            // Gün filtresi (boşsa tüm günler aktif)
            if (pref.ActiveDays.Count > 0 && !pref.ActiveDays.Contains(now.DayOfWeek))
                return false;

            // Saat filtresi
            var currentTime = TimeOnly.FromDateTime(now);
            if (pref.StartTime <= pref.EndTime)
                return currentTime >= pref.StartTime && currentTime <= pref.EndTime;
            else
                // Gece yarısını geçen aralık (örn. 22:00–06:00)
                return currentTime >= pref.StartTime || currentTime <= pref.EndTime;
        }
        catch
        {
            return true; // Timezone bulunamazsa engelleme
        }
    }

    private static List<string> BuildAlertReasons(
        NotificationPreference pref,
        Metar fresh,
        MetarComparison? diff)
    {
        var reasons = new List<string>();

        if (diff != null)
        {
            if (pref.NotifyOnCategoryChange && diff.CategoryChanged)
                reasons.Add($"Uçuş kategorisi değişti: {fresh.Category}");

            if (pref.NotifyOnVfrAchieved && diff.CategoryChanged
                && fresh.Category == FlightCategory.VFR)
                reasons.Add("VFR koşulları sağlandı");

            if (pref.NotifyOnSignificantWeather && diff.SignificantWeatherChanged)
                reasons.Add("Önemli meteorolojik olay");
        }

        if (pref.NotifyOnSpeci && fresh.IsSpeci)
            reasons.Add("SPECI raporu yayınlandı");

        if (pref.WindThresholdKnots.HasValue && fresh.WindSpeed >= pref.WindThresholdKnots)
            reasons.Add($"Rüzgar eşiği aşıldı: {fresh.WindSpeed}kt");

        if (pref.VisibilityThresholdMeters.HasValue
            && fresh.VisibilityMeters <= pref.VisibilityThresholdMeters)
            reasons.Add($"Görüş eşiği altına düştü: {fresh.VisibilityMeters}m");

        if (pref.CeilingThresholdFeet.HasValue
            && fresh.CeilingFeet <= pref.CeilingThresholdFeet)
            reasons.Add($"Tavan eşiği altına düştü: {fresh.CeilingFeet}ft");

        if (pref.NotifyOnEveryMetar)
            reasons.Add("Yeni METAR");

        return reasons;
    }

    private static object BuildMetarPayload(Metar m) => new
    {
        m.StationId,
        m.RawText,
        ObservationTime = m.ObservationTime.ToString("O"),
        m.WindDirection,
        m.WindSpeed,
        m.WindGust,
        m.IsVariableWind,
        m.VariableWindFrom,
        m.VariableWindTo,
        m.VisibilityMeters,
        m.CeilingFeet,
        Category = m.Category.ToString(),
        m.Temperature,
        m.DewPoint,
        m.AltimeterHpa,
        m.AltimeterInHg,
        m.Trend,
        m.IsSpeci,
        m.SourceProvider,
        FetchedAt = m.FetchedAt.ToString("O"),
        CloudLayers = m.CloudLayers.Select(c => new
        {
            Coverage = c.Coverage.ToString(),
            c.AltitudeFt,
            Type = c.Type.ToString()
        }),
        WeatherConditions = m.WeatherConditions.Select(w => w.ToString())
    };
}
