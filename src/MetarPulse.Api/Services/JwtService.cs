using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using MetarPulse.Core.Models;
using Microsoft.IdentityModel.Tokens;

namespace MetarPulse.Api.Services;

/// <summary>
/// Access token (JWT) + Refresh token üretimi ve doğrulaması.
/// Access token: 15 dakika. Refresh token: 30 gün, SHA256 hash olarak DB'de saklanır.
/// </summary>
public class JwtService
{
    private readonly JwtSettings _settings;
    private readonly SymmetricSecurityKey _signingKey;

    public JwtService(JwtSettings settings)
    {
        _settings = settings;
        _signingKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(settings.SecretKey));
    }

    // ── Access Token ─────────────────────────────────────────────────────────

    public string GenerateAccessToken(ApplicationUser user)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(JwtRegisteredClaimNames.Email, user.Email ?? ""),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("displayName", user.DisplayName ?? user.Email ?? ""),
            new("preferredUnits", user.PreferredUnits),
            new("preferredLanguage", user.PreferredLanguage)
        };

        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddMinutes(_settings.AccessTokenExpiryMinutes),
            signingCredentials: new SigningCredentials(_signingKey, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public ClaimsPrincipal? ValidateAccessToken(string token)
    {
        var validationParams = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = _signingKey,
            ValidateIssuer = true,
            ValidIssuer = _settings.Issuer,
            ValidateAudience = true,
            ValidAudience = _settings.Audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };

        try
        {
            return new JwtSecurityTokenHandler()
                .ValidateToken(token, validationParams, out _);
        }
        catch
        {
            return null;
        }
    }

    // ── Refresh Token ─────────────────────────────────────────────────────────

    /// <summary>Kriptografik güçlü rastgele refresh token üretir.</summary>
    public string GenerateRawRefreshToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(64);
        return Convert.ToBase64String(bytes);
    }

    /// <summary>Raw token'ın DB'ye kaydedilecek SHA256 hash'ini döndürür.</summary>
    public static string HashRefreshToken(string rawToken)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public RefreshToken CreateRefreshTokenEntity(
        string userId, string rawToken, string deviceInfo)
        => new()
        {
            UserId = userId,
            Token = HashRefreshToken(rawToken),
            DeviceInfo = deviceInfo,
            ExpiresAt = DateTime.UtcNow.AddDays(_settings.RefreshTokenExpiryDays),
            CreatedAt = DateTime.UtcNow
        };

    public DateTime AccessTokenExpiry => DateTime.UtcNow.AddMinutes(_settings.AccessTokenExpiryMinutes);
}

public class JwtSettings
{
    public string SecretKey { get; set; } = string.Empty;
    public string Issuer { get; set; } = "MetarPulse";
    public string Audience { get; set; } = "MetarPulseApp";
    public int AccessTokenExpiryMinutes { get; set; } = 15;
    public int RefreshTokenExpiryDays { get; set; } = 30;
}
