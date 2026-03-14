using System.Security.Claims;
using MetarPulse.Api.Services;
using MetarPulse.Core.Models;
using MetarPulse.Infrastructure.Persistence.PostgreSQL;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MetarPulse.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly JwtService _jwt;
    private readonly MagicLinkService _magicLink;
    private readonly AppDbContext _db;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        JwtService jwt,
        MagicLinkService magicLink,
        AppDbContext db,
        ILogger<AuthController> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _jwt = jwt;
        _magicLink = magicLink;
        _db = db;
        _logger = logger;
    }

    // ── Kayıt ─────────────────────────────────────────────────────────────────

    /// <summary>POST /api/auth/register — E-posta + şifre ile yeni hesap</summary>
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest req, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var existing = await _userManager.FindByEmailAsync(req.Email);
        if (existing != null)
            return Conflict(new { message = "Bu e-posta adresi zaten kayıtlı." });

        var user = new ApplicationUser
        {
            UserName = req.Email,
            Email = req.Email,
            DisplayName = req.DisplayName ?? req.Email.Split('@')[0],
            CreatedAt = DateTime.UtcNow
        };

        var result = await _userManager.CreateAsync(user, req.Password);
        if (!result.Succeeded)
            return BadRequest(new { errors = result.Errors.Select(e => e.Description) });

        // Pilot profili oluştur
        _db.PilotProfiles.Add(new PilotProfile { UserId = user.Id });
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Yeni kullanıcı: {Email}", req.Email);
        return Ok(await BuildTokenResponse(user, GetDeviceInfo(), ct));
    }

    // ── Giriş ─────────────────────────────────────────────────────────────────

    /// <summary>POST /api/auth/login — E-posta + şifre ile giriş</summary>
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req, CancellationToken ct)
    {
        var user = await _userManager.FindByEmailAsync(req.Email);
        if (user == null || user.IsDeleted)
            return Unauthorized(new { message = "Geçersiz e-posta veya şifre." });

        var result = await _signInManager.CheckPasswordSignInAsync(user, req.Password, lockoutOnFailure: true);
        if (!result.Succeeded)
        {
            if (result.IsLockedOut)
                return StatusCode(429, new { message = "Hesap kilitlendi. 15 dakika sonra tekrar deneyin." });
            return Unauthorized(new { message = "Geçersiz e-posta veya şifre." });
        }

        user.LastLoginAt = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);

        return Ok(await BuildTokenResponse(user, req.DeviceInfo ?? GetDeviceInfo(), ct));
    }

    // ── Token yenileme ────────────────────────────────────────────────────────

    /// <summary>POST /api/auth/refresh — Refresh token ile yeni access token al</summary>
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest req, CancellationToken ct)
    {
        var tokenHash = JwtService.HashRefreshToken(req.RefreshToken);

        var stored = await _db.RefreshTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.Token == tokenHash, ct);

        if (stored == null || !stored.IsActive)
            return Unauthorized(new { message = "Geçersiz veya süresi dolmuş refresh token." });

        if (stored.User.IsDeleted)
            return Unauthorized(new { message = "Hesap silinmiş." });

        // Eski token'ı iptal et, yeni üret (token rotation)
        stored.RevokedAt = DateTime.UtcNow;
        var newRaw = _jwt.GenerateRawRefreshToken();
        var newToken = _jwt.CreateRefreshTokenEntity(stored.UserId, newRaw, stored.DeviceInfo);
        _db.RefreshTokens.Add(newToken);
        await _db.SaveChangesAsync(ct);

        return Ok(new TokenResponse(
            _jwt.GenerateAccessToken(stored.User),
            newRaw,
            _jwt.AccessTokenExpiry,
            ToUserDto(stored.User)));
    }

    // ── Çıkış ─────────────────────────────────────────────────────────────────

    /// <summary>POST /api/auth/logout — Refresh token'ı iptal et</summary>
    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout([FromBody] LogoutRequest req, CancellationToken ct)
    {
        var tokenHash = JwtService.HashRefreshToken(req.RefreshToken);
        var stored = await _db.RefreshTokens
            .FirstOrDefaultAsync(t => t.Token == tokenHash, ct);

        if (stored != null)
        {
            stored.RevokedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }

        return NoContent();
    }

    // ── Magic Link ────────────────────────────────────────────────────────────

    /// <summary>POST /api/auth/magic-link/send — Magic link gönder</summary>
    [HttpPost("magic-link/send")]
    public async Task<IActionResult> SendMagicLink(
        [FromBody] MagicLinkSendRequest req,
        CancellationToken ct)
    {
        // Güvenlik: kullanıcı var mı yok mu açıklamayız
        _ = await _magicLink.SendMagicLinkAsync(req.Email, ct);
        return Ok(new { message = "Eğer bu e-posta kayıtlıysa, giriş bağlantısı gönderildi." });
    }

    /// <summary>GET /api/auth/magic-link/verify?token=... — Magic link doğrula</summary>
    [HttpGet("magic-link/verify")]
    public async Task<IActionResult> VerifyMagicLink(
        [FromQuery] string token,
        CancellationToken ct)
    {
        var email = await _magicLink.VerifyTokenAsync(token, ct);
        if (email == null)
            return BadRequest(new { message = "Geçersiz veya süresi dolmuş bağlantı." });

        // Kullanıcıyı bul veya oluştur
        var user = await _userManager.FindByEmailAsync(email);
        if (user == null)
        {
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                DisplayName = email.Split('@')[0],
                EmailConfirmed = true,
                CreatedAt = DateTime.UtcNow
            };
            var result = await _userManager.CreateAsync(user);
            if (!result.Succeeded)
                return StatusCode(500, new { message = "Kullanıcı oluşturulamadı." });

            _db.PilotProfiles.Add(new PilotProfile { UserId = user.Id });
            await _db.SaveChangesAsync(ct);
        }

        user.LastLoginAt = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);

        return Ok(await BuildTokenResponse(user, GetDeviceInfo(), ct));
    }

    // ── Profil ────────────────────────────────────────────────────────────────

    /// <summary>GET /api/auth/me — Mevcut kullanıcı bilgileri</summary>
    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? User.FindFirstValue("sub");
        if (userId == null) return Unauthorized();

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null || user.IsDeleted) return Unauthorized();

        return Ok(ToUserDto(user));
    }

    // ── Yardımcı metodlar ─────────────────────────────────────────────────────

    private async Task<TokenResponse> BuildTokenResponse(
        ApplicationUser user, string deviceInfo, CancellationToken ct)
    {
        var rawRefresh = _jwt.GenerateRawRefreshToken();
        var refreshEntity = _jwt.CreateRefreshTokenEntity(user.Id, rawRefresh, deviceInfo);
        _db.RefreshTokens.Add(refreshEntity);
        await _db.SaveChangesAsync(ct);

        return new TokenResponse(
            _jwt.GenerateAccessToken(user),
            rawRefresh,
            _jwt.AccessTokenExpiry,
            ToUserDto(user));
    }

    private string GetDeviceInfo()
    {
        var ua = Request.Headers.UserAgent.ToString();
        return string.IsNullOrEmpty(ua) ? "Unknown" : ua[..Math.Min(ua.Length, 200)];
    }

    private static UserDto ToUserDto(ApplicationUser u) => new(
        u.Id, u.Email ?? "", u.DisplayName, u.PreferredLanguage,
        u.PreferredUnits, u.IsOnboardingCompleted, u.CreatedAt);
}

// ── Request / Response kayıtları ─────────────────────────────────────────────

public record RegisterRequest(
    string Email,
    string Password,
    string? DisplayName = null);

public record LoginRequest(
    string Email,
    string Password,
    string? DeviceInfo = null);

public record RefreshRequest(string RefreshToken);
public record LogoutRequest(string RefreshToken);
public record MagicLinkSendRequest(string Email);

public record TokenResponse(
    string AccessToken,
    string RefreshToken,
    DateTime ExpiresAt,
    UserDto User);

public record UserDto(
    string Id,
    string Email,
    string? DisplayName,
    string PreferredLanguage,
    string PreferredUnits,
    bool IsOnboardingCompleted,
    DateTime CreatedAt);
