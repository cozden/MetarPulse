using MetarPulse.Api.Services;
using MetarPulse.Core.Models;
using MetarPulse.Infrastructure.Persistence.PostgreSQL;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MetarPulse.Api.Controllers;

[ApiController]
[Route("api/devices")]
[Authorize]
public class DevicesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly FcmService _fcm;

    public DevicesController(AppDbContext db, UserManager<ApplicationUser> userManager, FcmService fcm)
    {
        _db = db;
        _userManager = userManager;
        _fcm = fcm;
    }

    /// <summary>
    /// FCM token'ı kaydet veya güncelle (upsert).
    /// Login sonrası MAUI uygulaması bu endpoint'i çağırır.
    /// </summary>
    [HttpPost("token")]
    public async Task<IActionResult> RegisterToken(
        [FromBody] RegisterTokenRequest request,
        CancellationToken ct)
    {
        var userId = _userManager.GetUserId(User);
        if (userId == null) return Unauthorized();

        if (string.IsNullOrWhiteSpace(request.Token))
            return BadRequest("Token boş olamaz.");

        var platform = request.Platform?.ToLower() switch
        {
            "android" => "android",
            "ios"     => "ios",
            _         => "android"
        };

        // Upsert: token varsa güncelle, yoksa ekle
        var existing = await _db.DeviceTokens
            .FirstOrDefaultAsync(d => d.Token == request.Token, ct);

        if (existing != null)
        {
            existing.UserId    = userId;
            existing.Platform  = platform;
            existing.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            _db.DeviceTokens.Add(new DeviceToken
            {
                UserId    = userId,
                Token     = request.Token,
                Platform  = platform,
                UpdatedAt = DateTime.UtcNow
            });
        }

        await _db.SaveChangesAsync(ct);
        return Ok();
    }

    /// <summary>
    /// Logout veya uygulama kaldırıldığında token'ı sil.
    /// </summary>
    [HttpDelete("token")]
    public async Task<IActionResult> UnregisterToken(
        [FromBody] UnregisterTokenRequest request,
        CancellationToken ct)
    {
        var userId = _userManager.GetUserId(User);
        if (userId == null) return Unauthorized();

        var token = await _db.DeviceTokens
            .FirstOrDefaultAsync(d => d.Token == request.Token && d.UserId == userId, ct);

        if (token != null)
        {
            _db.DeviceTokens.Remove(token);
            await _db.SaveChangesAsync(ct);
        }

        return Ok();
    }

    /// <summary>
    /// POST /api/devices/test-push — Mevcut kullanıcının tüm cihazlarına test bildirimi gönderir.
    /// </summary>
    [HttpPost("test-push")]
    public async Task<IActionResult> TestPush(CancellationToken ct)
    {
        var userId = _userManager.GetUserId(User);
        if (userId == null) return Unauthorized();

        var tokens = await _db.DeviceTokens
            .Where(d => d.UserId == userId)
            .Select(d => d.Token)
            .ToListAsync(ct);

        if (tokens.Count == 0)
            return NotFound(new { message = "Bu kullanıcıya ait kayıtlı cihaz token'ı bulunamadı." });

        var invalid = await _fcm.SendMulticastAsync(
            tokens,
            title: "MetarPulse Test",
            body:  "FCM bildirimi çalışıyor! ✓",
            data:  new Dictionary<string, string> { ["type"] = "test" },
            ct:    ct);

        return Ok(new
        {
            tokenCount   = tokens.Count,
            invalidCount = invalid.Count,
            message      = invalid.Count == tokens.Count
                ? "Tüm token'lar geçersiz — FCM'e ulaşılamıyor veya token süresi dolmuş."
                : $"{tokens.Count - invalid.Count}/{tokens.Count} cihaza gönderildi."
        });
    }
}

public record RegisterTokenRequest(string Token, string? Platform);
public record UnregisterTokenRequest(string Token);
