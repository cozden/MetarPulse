using System.Security.Claims;
using MetarPulse.Core.Models;
using MetarPulse.Infrastructure.Persistence.PostgreSQL;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MetarPulse.Api.Controllers;

[ApiController]
[Route("api/notifications")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly AppDbContext _db;

    public NotificationsController(AppDbContext db) => _db = db;

    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.FindFirstValue("sub")
        ?? throw new UnauthorizedAccessException();

    /// <summary>GET /api/notifications — Kullanıcının bildirim tercihleri</summary>
    [HttpGet]
    public async Task<IActionResult> GetPreferences(CancellationToken ct)
    {
        var prefs = await _db.NotificationPreferences
            .Where(p => p.UserId == UserId)
            .OrderBy(p => p.StationIcao)
            .ToListAsync(ct);

        return Ok(prefs.Select(ToDto));
    }

    /// <summary>GET /api/notifications/{icao} — Belirli istasyon tercihi</summary>
    [HttpGet("{icao}")]
    public async Task<IActionResult> GetPreference(string icao, CancellationToken ct)
    {
        var pref = await _db.NotificationPreferences
            .FirstOrDefaultAsync(p => p.UserId == UserId
                && p.StationIcao == icao.ToUpperInvariant(), ct);

        return pref is null
            ? NotFound(new { message = $"{icao} için bildirim tercihi bulunamadı." })
            : Ok(ToDto(pref));
    }

    /// <summary>POST /api/notifications — Bildirim tercihi oluştur veya güncelle (upsert)</summary>
    [HttpPost]
    public async Task<IActionResult> UpsertPreference(
        [FromBody] NotificationPreferenceRequest req,
        CancellationToken ct)
    {
        var icao = req.StationIcao.ToUpperInvariant();

        var existing = await _db.NotificationPreferences
            .FirstOrDefaultAsync(p => p.UserId == UserId && p.StationIcao == icao, ct);

        if (existing is null)
        {
            existing = new NotificationPreference { UserId = UserId, StationIcao = icao };
            _db.NotificationPreferences.Add(existing);
        }

        // Apply
        existing.NotifyOnCategoryChange    = req.NotifyOnCategoryChange;
        existing.NotifyOnSpeci             = req.NotifyOnSpeci;
        existing.NotifyOnVfrAchieved       = req.NotifyOnVfrAchieved;
        existing.NotifyOnSignificantWeather = req.NotifyOnSignificantWeather;
        existing.NotifyOnEveryMetar        = req.NotifyOnEveryMetar;
        existing.WindThresholdKnots        = req.WindThresholdKnots;
        existing.VisibilityThresholdMeters = req.VisibilityThresholdMeters;
        existing.CeilingThresholdFeet      = req.CeilingThresholdFeet;

        await _db.SaveChangesAsync(ct);
        return Ok(ToDto(existing));
    }

    /// <summary>DELETE /api/notifications/{icao} — Bildirim tercihini sil</summary>
    [HttpDelete("{icao}")]
    public async Task<IActionResult> DeletePreference(string icao, CancellationToken ct)
    {
        var pref = await _db.NotificationPreferences
            .FirstOrDefaultAsync(p => p.UserId == UserId
                && p.StationIcao == icao.ToUpperInvariant(), ct);

        if (pref is null) return NotFound();

        _db.NotificationPreferences.Remove(pref);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    private static NotificationPreferenceDto ToDto(NotificationPreference p) => new(
        p.StationIcao,
        p.NotifyOnCategoryChange,
        p.NotifyOnSpeci,
        p.NotifyOnVfrAchieved,
        p.NotifyOnSignificantWeather,
        p.NotifyOnEveryMetar,
        p.WindThresholdKnots,
        p.VisibilityThresholdMeters,
        p.CeilingThresholdFeet);
}

// ── DTO'lar ───────────────────────────────────────────────────────────────────

public record NotificationPreferenceDto(
    string StationIcao,
    bool NotifyOnCategoryChange,
    bool NotifyOnSpeci,
    bool NotifyOnVfrAchieved,
    bool NotifyOnSignificantWeather,
    bool NotifyOnEveryMetar,
    int? WindThresholdKnots,
    int? VisibilityThresholdMeters,
    int? CeilingThresholdFeet);

public record NotificationPreferenceRequest(
    string StationIcao,
    bool NotifyOnCategoryChange = true,
    bool NotifyOnSpeci = true,
    bool NotifyOnVfrAchieved = true,
    bool NotifyOnSignificantWeather = true,
    bool NotifyOnEveryMetar = false,
    int? WindThresholdKnots = null,
    int? VisibilityThresholdMeters = null,
    int? CeilingThresholdFeet = null);
