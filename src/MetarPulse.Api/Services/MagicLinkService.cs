using System.Security.Cryptography;
using MetarPulse.Core.Models;
using MetarPulse.Infrastructure.Persistence.PostgreSQL;
using Microsoft.EntityFrameworkCore;

namespace MetarPulse.Api.Services;

/// <summary>
/// Magic Link kimlik doğrulama servisi.
/// Token üretir, DB'ye kaydeder ve (stub) e-posta gönderir.
/// Token 15 dakika geçerlidir, tek kullanımlıktır.
/// </summary>
public class MagicLinkService
{
    private readonly AppDbContext _db;
    private readonly ILogger<MagicLinkService> _logger;

    // Gerçek uygulamada IEmailService inject edilir
    private readonly string _baseUrl;

    private static readonly TimeSpan TokenTtl = TimeSpan.FromMinutes(15);

    public MagicLinkService(
        AppDbContext db,
        ILogger<MagicLinkService> logger,
        IConfiguration configuration)
    {
        _db = db;
        _logger = logger;
        _baseUrl = configuration["App:BaseUrl"] ?? "https://localhost:5001";
    }

    /// <summary>
    /// Verilen e-posta için magic link token üretir ve "gönderir".
    /// Gerçek uygulamada SMTP/SendGrid ile e-posta gönderilir.
    /// </summary>
    public async Task<string> SendMagicLinkAsync(string email, CancellationToken ct = default)
    {
        // Eskimiş, kullanılmamış tokenları temizle
        var expired = await _db.MagicLinkTokens
            .Where(t => t.Email == email && (t.IsUsed || t.ExpiresAt < DateTime.UtcNow))
            .ToListAsync(ct);
        _db.MagicLinkTokens.RemoveRange(expired);

        // Yeni token oluştur
        var rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace('+', '-').Replace('/', '_').TrimEnd('='); // URL-safe

        var entity = new MagicLinkToken
        {
            Email = email.ToLowerInvariant(),
            Token = rawToken,
            ExpiresAt = DateTime.UtcNow.Add(TokenTtl),
            CreatedAt = DateTime.UtcNow
        };

        _db.MagicLinkTokens.Add(entity);
        await _db.SaveChangesAsync(ct);

        var link = $"{_baseUrl}/api/auth/magic-link/verify?token={rawToken}";

        // TODO: Gerçek e-posta gönderimi (SendGrid/SMTP)
        _logger.LogInformation(
            "Magic link oluşturuldu: {Email} → {Link} (geçerlilik: {Min} dk)",
            email, link, TokenTtl.TotalMinutes);

        return rawToken; // Test/dev ortamında token döndürülür
    }

    /// <summary>
    /// Token'ı doğrular. Geçerliyse e-posta adresini döndürür, aksi hâlde null.
    /// Token tek kullanımlıktır — doğrulama sonrası işaretlenir.
    /// </summary>
    public async Task<string?> VerifyTokenAsync(string rawToken, CancellationToken ct = default)
    {
        var entity = await _db.MagicLinkTokens
            .FirstOrDefaultAsync(t => t.Token == rawToken, ct);

        if (entity == null || !entity.IsValid)
            return null;

        entity.IsUsed = true;
        await _db.SaveChangesAsync(ct);

        return entity.Email;
    }
}
