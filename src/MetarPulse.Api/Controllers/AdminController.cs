using MetarPulse.Abstractions.Providers;
using MetarPulse.Api.Services;
using MetarPulse.Core.Models;
using MetarPulse.Infrastructure.Persistence.PostgreSQL;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MetarPulse.Api.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Policy = "AdminAccess")]
public class AdminController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly IProviderManager _providerManager;
    private readonly INotamProvider _notamProvider;
    private readonly AdminLogBuffer _logBuffer;
    private readonly SystemSettingsService _sysSettings;
    private readonly IConfiguration _config;
    private readonly WeatherProviderSettings _providerSettings;

    public AdminController(
        AppDbContext db,
        UserManager<ApplicationUser> users,
        IProviderManager providerManager,
        INotamProvider notamProvider,
        AdminLogBuffer logBuffer,
        SystemSettingsService sysSettings,
        IConfiguration config,
        IOptions<WeatherProviderSettings> providerOptions)
    {
        _db = db;
        _users = users;
        _providerManager = providerManager;
        _notamProvider = notamProvider;
        _logBuffer = logBuffer;
        _sysSettings = sysSettings;
        _config = config;
        _providerSettings = providerOptions.Value;
    }

    /// <summary>GET /api/admin/stats — Dashboard özet istatistikleri</summary>
    [HttpGet("stats")]
    public async Task<IActionResult> GetStats(CancellationToken ct)
    {
        var since24h = DateTime.UtcNow.AddHours(-24);

        var totalUsers      = await _db.Users.CountAsync(u => !((ApplicationUser)u).IsDeleted, ct);
        var activeBookmarks = await _db.UserBookmarks.CountAsync(ct);
        var metarsLast24h   = await _db.MetarHistory.CountAsync(m => m.ObservationTime >= since24h, ct);
        var notamCount      = await _db.Notams.CountAsync(ct);

        var providerHealth = _providerManager.GetHealthStatuses();
        var healthyCount   = providerHealth.Count(h => h.IsHealthy);

        return Ok(new
        {
            TotalUsers      = totalUsers,
            ActiveBookmarks = activeBookmarks,
            MetarsLast24h   = metarsLast24h,
            NotamCount      = notamCount,
            HealthyProviders = healthyCount,
            TotalProviders   = providerHealth.Count
        });
    }

    /// <summary>GET /api/admin/users — Kullanıcı listesi</summary>
    [HttpGet("users")]
    public async Task<IActionResult> GetUsers(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        page = Math.Max(1, page);

        var query = _db.Users
            .OfType<ApplicationUser>()
            .Where(u => !u.IsDeleted);

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(u =>
                u.Email!.Contains(search) ||
                (u.DisplayName != null && u.DisplayName.Contains(search)));

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(u => u.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new
            {
                u.Id,
                u.Email,
                u.DisplayName,
                u.CreatedAt,
                u.LastLoginAt,
                u.IsOnboardingCompleted
            })
            .ToListAsync(ct);

        // Rolleri ayrı sorgula (Identity join)
        var result = new List<object>();
        foreach (var u in items)
        {
            var user = await _users.FindByIdAsync(u.Id);
            var roles = user != null ? await _users.GetRolesAsync(user) : [];
            result.Add(new
            {
                u.Id,
                u.Email,
                u.DisplayName,
                CreatedAt = u.CreatedAt.ToString("O"),
                LastLoginAt = u.LastLoginAt?.ToString("O"),
                u.IsOnboardingCompleted,
                Roles = roles
            });
        }

        return Ok(new { Total = total, Page = page, PageSize = pageSize, Items = result });
    }

    /// <summary>PUT /api/admin/users/{id}/role — Rol değiştir (User ↔ Admin)</summary>
    [HttpPut("users/{id}/role")]
    public async Task<IActionResult> SetRole(string id, [FromQuery] string role)
    {
        if (role != "Admin" && role != "User")
            return BadRequest(new { message = "Geçerli roller: Admin, User" });

        var user = await _users.FindByIdAsync(id);
        if (user == null || user.IsDeleted)
            return NotFound(new { message = "Kullanıcı bulunamadı" });

        var currentRoles = await _users.GetRolesAsync(user);
        await _users.RemoveFromRolesAsync(user, currentRoles);
        await _users.AddToRoleAsync(user, role);

        return Ok(new { message = $"{user.Email} → {role}" });
    }

    /// <summary>DELETE /api/admin/users/{id} — Kullanıcıyı soft-delete</summary>
    [HttpDelete("users/{id}")]
    public async Task<IActionResult> DeleteUser(string id)
    {
        var user = await _users.FindByIdAsync(id);
        if (user == null || user.IsDeleted)
            return NotFound(new { message = "Kullanıcı bulunamadı" });

        user.IsDeleted = true;
        user.DeletedAt = DateTime.UtcNow;
        await _users.UpdateAsync(user);

        return Ok(new { message = $"{user.Email} silindi" });
    }

    // ── Cache yönetimi ────────────────────────────────────────────────────────

    /// <summary>GET /api/admin/cache — Her istasyon için en güncel METAR kaydı</summary>
    [HttpGet("cache")]
    public async Task<IActionResult> GetCache(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        pageSize = Math.Clamp(pageSize, 1, 200);
        page = Math.Max(1, page);

        // Her istasyon için en güncel METAR'ı al (EF Core GroupBy+First SQL'e çevrilemiyor, join kullan)
        var latestTimes = _db.MetarHistory
            .GroupBy(m => m.StationId)
            .Select(g => new { StationId = g.Key, MaxTime = g.Max(m => m.ObservationTime) });

        var latestQuery = _db.MetarHistory
            .Join(latestTimes,
                m  => new { m.StationId, m.ObservationTime },
                lt => new { lt.StationId, ObservationTime = lt.MaxTime },
                (m, _) => m);

        if (!string.IsNullOrWhiteSpace(search))
            latestQuery = latestQuery.Where(m => m.StationId.Contains(search.ToUpper()));

        var total = await latestQuery.CountAsync(ct);

        var items = await latestQuery
            .OrderByDescending(m => m.FetchedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(m => new
            {
                m.StationId,
                ObservationTime = m.ObservationTime.ToString("O"),
                FetchedAt       = m.FetchedAt.ToString("O"),
                m.SourceProvider,
                m.RawText,
                Category        = m.Category.ToString()
            })
            .ToListAsync(ct);

        return Ok(new { Total = total, Page = page, PageSize = pageSize, Items = items });
    }

    /// <summary>POST /api/admin/cache/{icao}/refresh — Tek meydan için METAR yenile</summary>
    [HttpPost("cache/{icao}/refresh")]
    public async Task<IActionResult> RefreshStation(string icao, CancellationToken ct)
    {
        icao = icao.ToUpperInvariant();
        var metar = await _providerManager.GetMetarWithFallbackAsync(icao, ct);
        if (metar == null)
            return NotFound(new { message = $"{icao} için METAR alınamadı" });

        _db.MetarHistory.Add(metar);
        await _db.SaveChangesAsync(ct);

        return Ok(new
        {
            message      = $"{icao} yenilendi",
            provider     = metar.SourceProvider,
            observedAt   = metar.ObservationTime.ToString("O")
        });
    }

    /// <summary>POST /api/admin/cache/refresh-all — Tüm aktif (bookmark'lı) meydanları yenile</summary>
    [HttpPost("cache/refresh-all")]
    public async Task<IActionResult> RefreshAll(CancellationToken ct)
    {
        var icaos = await _db.UserBookmarks
            .Select(b => b.StationIcao)
            .Distinct()
            .ToListAsync(ct);

        if (icaos.Count == 0)
            return Ok(new { message = "Yenilenecek bookmark bulunamadı", refreshed = 0 });

        var tasks   = icaos.Select(icao => _providerManager.GetMetarWithFallbackAsync(icao, ct));
        var results = await Task.WhenAll(tasks);

        var fetched = results.Where(r => r != null).Cast<Metar>().ToList();
        if (fetched.Count > 0)
        {
            _db.MetarHistory.AddRange(fetched);
            await _db.SaveChangesAsync(ct);
        }

        return Ok(new { message = $"{fetched.Count}/{icaos.Count} meydan yenilendi", refreshed = fetched.Count });
    }

    /// <summary>DELETE /api/admin/cache/{icao} — Bir meydanın tüm METAR geçmişini sil</summary>
    [HttpDelete("cache/{icao}")]
    public async Task<IActionResult> ClearStationCache(string icao, CancellationToken ct)
    {
        icao = icao.ToUpperInvariant();
        var count = await _db.MetarHistory
            .Where(m => m.StationId == icao)
            .ExecuteDeleteAsync(ct);

        return Ok(new { message = $"{icao}: {count} kayıt silindi", deleted = count });
    }

    // ── NOTAM yönetimi ────────────────────────────────────────────────────────

    /// <summary>GET /api/admin/notams — NOTAM listesi (filtre, sayfalama)</summary>
    [HttpGet("notams")]
    public async Task<IActionResult> GetNotams(
        [FromQuery] string? icao,
        [FromQuery] string? impact,     // None|Advisory|Caution|Warning
        [FromQuery] bool? activeOnly,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        pageSize = Math.Clamp(pageSize, 1, 200);
        page     = Math.Max(1, page);

        var query = _db.Notams.AsQueryable();

        if (!string.IsNullOrWhiteSpace(icao))
            query = query.Where(n => n.AirportIdent == icao.ToUpperInvariant());

        if (activeOnly == true)
            query = query.Where(n => n.IsPermanent || n.EffectiveTo == null || n.EffectiveTo > DateTime.UtcNow);

        if (!string.IsNullOrWhiteSpace(impact) &&
            Enum.TryParse<Core.Enums.NotamVfrImpact>(impact, true, out var parsedImpact))
            query = query.Where(n => n.VfrImpact == parsedImpact);

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(n => n.EffectiveFrom)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(n => new
            {
                n.Id,
                n.NotamId,
                n.AirportIdent,
                n.FirIdent,
                NotamType      = n.NotamType.ToString(),
                VfrImpact      = n.VfrImpact.ToString(),
                Traffic        = n.Traffic.ToString(),
                EffectiveFrom  = n.EffectiveFrom.ToString("O"),
                EffectiveTo    = n.EffectiveTo.HasValue ? n.EffectiveTo.Value.ToString("O") : null,
                n.IsPermanent,
                n.Subject,
                n.LowerLimit,
                n.UpperLimit,
                n.RawText,
                n.SourceProvider,
                FetchedAt      = n.FetchedAt.ToString("O")
            })
            .ToListAsync(ct);

        return Ok(new { Total = total, Page = page, PageSize = pageSize, Items = items });
    }

    /// <summary>POST /api/admin/notams/poll — Bookmark'lı meydanlar için NOTAM polling</summary>
    [HttpPost("notams/poll")]
    public async Task<IActionResult> PollNotams(CancellationToken ct)
    {
        var icaos = await _db.UserBookmarks
            .Select(b => b.StationIcao)
            .Distinct()
            .ToListAsync(ct);

        if (icaos.Count == 0)
            return Ok(new { message = "Yenilenecek bookmark bulunamadı", fetched = 0 });

        var notams = await _notamProvider.GetNotamsAsync(icaos, ct);

        // Daha önce çekilmemiş olanları ekle (NotamId + AirportIdent unique)
        var existingIds = await _db.Notams
            .Where(n => icaos.Contains(n.AirportIdent))
            .Select(n => n.NotamId)
            .ToHashSetAsync(ct);

        var newNotams = notams
            .Where(n => !existingIds.Contains(n.NotamId))
            .ToList();

        if (newNotams.Count > 0)
        {
            _db.Notams.AddRange(newNotams);
            await _db.SaveChangesAsync(ct);
        }

        return Ok(new
        {
            message = $"{newNotams.Count} yeni NOTAM eklendi ({notams.Count} toplam çekildi)",
            fetched = notams.Count,
            added   = newNotams.Count
        });
    }

    /// <summary>DELETE /api/admin/notams/{id} — Tek NOTAM sil</summary>
    [HttpDelete("notams/{id:int}")]
    public async Task<IActionResult> DeleteNotam(int id, CancellationToken ct)
    {
        var deleted = await _db.Notams
            .Where(n => n.Id == id)
            .ExecuteDeleteAsync(ct);

        return deleted > 0
            ? Ok(new { message = $"NOTAM #{id} silindi" })
            : NotFound(new { message = $"NOTAM #{id} bulunamadı" });
    }

    /// <summary>DELETE /api/admin/notams/expired — Süresi geçmiş tüm NOTAM'ları temizle</summary>
    [HttpDelete("notams/expired")]
    public async Task<IActionResult> PurgeExpiredNotams(CancellationToken ct)
    {
        var count = await _db.Notams
            .Where(n => !n.IsPermanent && n.EffectiveTo < DateTime.UtcNow)
            .ExecuteDeleteAsync(ct);

        return Ok(new { message = $"{count} süresi dolmuş NOTAM silindi", deleted = count });
    }

    // ── Log görüntüleyici ─────────────────────────────────────────────────────

    /// <summary>GET /api/admin/logs — In-memory log buffer sorgula</summary>
    [HttpGet("logs")]
    public IActionResult GetLogs(
        [FromQuery] string? level,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] string? source,
        [FromQuery] int limit = 200)
    {
        limit = Math.Clamp(limit, 1, 500);
        var entries = _logBuffer.Query(level, from, to, source, limit);
        return Ok(entries.Select(e => new
        {
            Timestamp = e.Timestamp.ToString("O"),
            e.Level,
            e.Message,
            e.Source,
            e.Exception
        }));
    }

    /// <summary>DELETE /api/admin/logs — Log buffer'ı temizle</summary>
    [HttpDelete("logs")]
    public IActionResult ClearLogs()
    {
        _logBuffer.Clear();
        return Ok(new { message = "Log buffer temizlendi" });
    }

    // ── Sistem ayarları ───────────────────────────────────────────────────────

    /// <summary>GET /api/admin/settings — Sistem durumu ve ayarları</summary>
    [HttpGet("settings")]
    public async Task<IActionResult> GetSettings(CancellationToken ct)
    {
        // FCM durumu
        var fcmKeyPath = _config["Firebase:ServiceAccountPath"];
        var fcmConfigured = !string.IsNullOrWhiteSpace(fcmKeyPath) &&
                            System.IO.File.Exists(fcmKeyPath);

        // Provider API key durumu
        var providerKeys = new Dictionary<string, bool>
        {
            ["AVWX"]   = !string.IsNullOrWhiteSpace(_config["WeatherProviders:Providers:AVWX:ApiKey"]),
            ["CheckWX"] = !string.IsNullOrWhiteSpace(_config["WeatherProviders:Providers:CheckWX:ApiKey"])
        };

        // DB bağlantı testi
        bool dbOk;
        try { await _db.Database.CanConnectAsync(ct); dbOk = true; }
        catch { dbOk = false; }

        return Ok(new
        {
            System = new
            {
                Environment    = System.Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production",
                DotNetVersion  = System.Environment.Version.ToString(),
                StartedAt      = _sysSettings.StartedAt.ToString("O"),
                UptimeSeconds  = (int)_sysSettings.Uptime.TotalSeconds,
                MaintenanceMode = _sysSettings.MaintenanceMode
            },
            Database = new { IsConnected = dbOk },
            Fcm = new { IsConfigured = fcmConfigured },
            ProviderApiKeys = providerKeys,
            Polling = new
            {
                MetarCheckIntervalSeconds = 15,
                MetarIdleIntervalMinutes  = 5,
                MetarHotIntervalSeconds   = 15,
                NotamPollIntervalMinutes  = 30
            }
        });
    }

    /// <summary>PUT /api/admin/settings/maintenance — Bakım modunu aç/kapat</summary>
    [HttpPut("settings/maintenance")]
    public IActionResult SetMaintenance([FromQuery] bool enabled)
    {
        _sysSettings.MaintenanceMode = enabled;
        return Ok(new { message = $"Bakım modu → {(enabled ? "AÇIK" : "KAPALI")}", maintenanceMode = enabled });
    }

    // ── Provider ayarları ────────────────────────────────────────────────────

    /// <summary>GET /api/admin/providers/{name}/settings — Provider'ın mevcut ayarlarını döner (DB override + appsettings birleşimi)</summary>
    [HttpGet("providers/{name}/settings")]
    public async Task<IActionResult> GetProviderSettings(string name, CancellationToken ct)
    {
        var provider = _providerManager.GetAllWeatherProviders()
            .FirstOrDefault(p => p.ProviderName.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (provider == null) return NotFound(new { message = $"Provider bulunamadı: {name}" });

        var dbOverride = await _db.ProviderSettingOverrides
            .FirstOrDefaultAsync(o => o.ProviderName == name, ct);

        // Aktif config — WeatherProviderSettings (appsettings + runtime override birleşimi) üzerinden oku
        _providerSettings.Providers.TryGetValue(name, out var cfg);
        var activeConfig = cfg == null ? null : new
        {
            cfg.BaseUrl,
            ApiKeySet                    = !string.IsNullOrWhiteSpace(cfg.ApiKey),
            cfg.TimeoutSeconds,
            cfg.RetryCount,
            cfg.CircuitBreakerThreshold,
            cfg.CircuitBreakerDurationSeconds
        };

        var current = new
        {
            ProviderName  = provider.ProviderName,
            Enabled       = provider.IsEnabled,
            Priority      = provider.Priority,
            HasDbOverride = dbOverride != null,
            Active        = activeConfig,
            Override = dbOverride == null ? null : new
            {
                dbOverride.Enabled,
                dbOverride.Priority,
                dbOverride.BaseUrl,
                ApiKeySet = !string.IsNullOrWhiteSpace(dbOverride.ApiKey),
                dbOverride.TimeoutSeconds,
                dbOverride.RetryCount,
                dbOverride.CircuitBreakerThreshold,
                dbOverride.CircuitBreakerDurationSeconds,
                dbOverride.UpdatedAt,
                dbOverride.UpdatedBy
            }
        };
        return Ok(current);
    }

    /// <summary>PUT /api/admin/providers/{name}/settings — Provider ayarlarını DB'ye kaydet ve runtime'a uygula</summary>
    [HttpPut("providers/{name}/settings")]
    public async Task<IActionResult> UpdateProviderSettings(
        string name,
        [FromBody] ProviderSettingsRequest req,
        CancellationToken ct)
    {
        var provider = _providerManager.GetAllWeatherProviders()
            .FirstOrDefault(p => p.ProviderName.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (provider == null) return NotFound(new { message = $"Provider bulunamadı: {name}" });

        // DB override kaydet / güncelle
        var existing = await _db.ProviderSettingOverrides
            .FirstOrDefaultAsync(o => o.ProviderName == name, ct);

        if (existing == null)
        {
            existing = new ProviderSettingOverride { ProviderName = name };
            _db.ProviderSettingOverrides.Add(existing);
        }

        existing.Enabled                     = req.Enabled;
        existing.Priority                    = req.Priority;
        existing.BaseUrl                     = req.BaseUrl;
        existing.TimeoutSeconds              = req.TimeoutSeconds;
        existing.RetryCount                  = req.RetryCount;
        existing.CircuitBreakerThreshold     = req.CircuitBreakerThreshold;
        existing.CircuitBreakerDurationSeconds = req.CircuitBreakerDurationSeconds;
        existing.UpdatedAt                   = DateTime.UtcNow;
        existing.UpdatedBy                   = User.Identity?.Name ?? "admin";

        // ApiKey: boş string gelirse mevcut değeri koru
        if (!string.IsNullOrWhiteSpace(req.ApiKey))
            existing.ApiKey = req.ApiKey;

        await _db.SaveChangesAsync(ct);

        // WeatherProviderSettings (in-memory singleton) güncelle → tüm provider instance'ları etkiler
        if (_providerSettings.Providers.TryGetValue(name, out var activeCfg))
        {
            if (existing.Enabled                     != null) activeCfg.Enabled                     = existing.Enabled.Value;
            if (existing.Priority                    != null) activeCfg.Priority                    = existing.Priority.Value;
            if (!string.IsNullOrWhiteSpace(existing.BaseUrl)) activeCfg.BaseUrl                    = existing.BaseUrl;
            if (!string.IsNullOrWhiteSpace(existing.ApiKey))  activeCfg.ApiKey                     = existing.ApiKey;
            if (existing.TimeoutSeconds              != null) activeCfg.TimeoutSeconds              = existing.TimeoutSeconds.Value;
            if (existing.RetryCount                  != null) activeCfg.RetryCount                  = existing.RetryCount.Value;
            if (existing.CircuitBreakerThreshold     != null) activeCfg.CircuitBreakerThreshold     = existing.CircuitBreakerThreshold.Value;
            if (existing.CircuitBreakerDurationSeconds != null) activeCfg.CircuitBreakerDurationSeconds = existing.CircuitBreakerDurationSeconds.Value;
        }

        return Ok(new { message = $"{name} ayarları güncellendi ve uygulandı" });
    }

    /// <summary>DELETE /api/admin/providers/{name}/settings — Override'ı sil, appsettings.json değerlerine dön</summary>
    [HttpDelete("providers/{name}/settings")]
    public async Task<IActionResult> ResetProviderSettings(string name, CancellationToken ct)
    {
        var deleted = await _db.ProviderSettingOverrides
            .Where(o => o.ProviderName == name)
            .ExecuteDeleteAsync(ct);

        return Ok(new { message = deleted > 0
            ? $"{name} override'ı silindi — appsettings.json değerleri aktif"
            : $"{name} için override bulunamadı" });
    }

}

public record ProviderSettingsRequest(
    bool?   Enabled,
    int?    Priority,
    string? BaseUrl,
    string? ApiKey,
    int?    TimeoutSeconds,
    int?    RetryCount,
    int?    CircuitBreakerThreshold,
    int?    CircuitBreakerDurationSeconds);
