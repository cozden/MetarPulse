using MetarPulse.Abstractions.Providers;
using MetarPulse.Core.Enums;
using MetarPulse.Core.Models;
using MetarPulse.Infrastructure.Persistence.PostgreSQL;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace MetarPulse.Api.Controllers;

[ApiController]
[Route("api/notam")]
public class NotamController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly INotamAggregator _notamProvider;
    private readonly IMemoryCache _cache;
    private readonly ILogger<NotamController> _logger;

    // NOTAM'lar 30 dakika cache'lenir
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(30);

    public NotamController(
        AppDbContext db,
        INotamAggregator notamProvider,
        IMemoryCache cache,
        ILogger<NotamController> logger)
    {
        _db = db;
        _notamProvider = notamProvider;
        _cache = cache;
        _logger = logger;
    }

    /// <summary>GET /api/notam/{icao} — Meydanın aktif NOTAM listesi (cache'den veya provider'dan)</summary>
    [HttpGet("{icao}")]
    public async Task<IActionResult> GetNotams(string icao, CancellationToken ct)
    {
        icao = icao.ToUpperInvariant();
        var cacheKey = $"notam:{icao}";

        if (_cache.TryGetValue(cacheKey, out List<NotamDto>? cached) && cached is not null)
            return Ok(cached);

        var notams = await FetchAndStoreAsync(icao, ct);
        var dtos = notams.Select(ToDto).ToList();

        _cache.Set(cacheKey, dtos, CacheDuration);
        return Ok(dtos);
    }

    /// <summary>GET /api/notam/bulk?icaos=LTFM,EGLL — Çoklu meydan için NOTAM özeti</summary>
    [HttpGet("bulk")]
    public async Task<IActionResult> GetBulk([FromQuery] string icaos, CancellationToken ct)
    {
        var codes = icaos.Split(',', StringSplitOptions.RemoveEmptyEntries)
                         .Select(c => c.Trim().ToUpperInvariant())
                         .Take(20)
                         .ToList();

        if (codes.Count == 0)
            return BadRequest(new { message = "En az bir ICAO kodu gerekli." });

        var result = new List<NotamSummaryDto>();

        foreach (var icao in codes)
        {
            var cacheKey = $"notam:{icao}";
            List<NotamDto>? notams = null;

            if (!_cache.TryGetValue(cacheKey, out notams) || notams is null)
            {
                var fetched = await FetchAndStoreAsync(icao, ct);
                notams = fetched.Select(ToDto).ToList();
                _cache.Set(cacheKey, notams, CacheDuration);
            }

            result.Add(new NotamSummaryDto(
                icao,
                notams.Count,
                notams.Any(n => n.VfrImpact == nameof(NotamVfrImpact.OperationsCritical)),
                notams.Any(n => n.VfrImpact == nameof(NotamVfrImpact.Warning)),
                notams.Any(n => n.VfrImpact == nameof(NotamVfrImpact.Caution))
            ));
        }

        return Ok(result);
    }

    /// <summary>POST /api/notam/{icao}/refresh — Cache'i temizle ve provider'dan yeniden çek</summary>
    [HttpPost("{icao}/refresh")]
    public async Task<IActionResult> Refresh(string icao, CancellationToken ct)
    {
        icao = icao.ToUpperInvariant();
        _cache.Remove($"notam:{icao}");

        var notams = await FetchAndStoreAsync(icao, ct);
        var dtos = notams.Select(ToDto).ToList();

        _cache.Set($"notam:{icao}", dtos, CacheDuration);
        _logger.LogInformation("NOTAM manuel yenilendi: {ICAO} — {Count} NOTAM", icao, dtos.Count);

        return Ok(new { icao, count = dtos.Count, notams = dtos });
    }

    // ── İç metodlar ──────────────────────────────────────────────────────────

    private async Task<List<Notam>> FetchAndStoreAsync(string icao, CancellationToken ct)
    {
        try
        {
            var fresh = await _notamProvider.GetNotamsAsync(icao, ct);

            if (fresh.Count > 0)
            {
                // Eski NOTAM'ları sil, yenilerini kaydet (upsert yerine temizle+ekle)
                var existing = await _db.Notams
                    .Where(n => n.AirportIdent == icao)
                    .ToListAsync(ct);

                _db.Notams.RemoveRange(existing);

                await _db.Notams.AddRangeAsync(fresh, ct);
                await _db.SaveChangesAsync(ct);
            }

            // DB'den aktif olanları döndür
            return await _db.Notams
                .Where(n => n.AirportIdent == icao
                         && (n.EffectiveTo == null || n.EffectiveTo > DateTime.UtcNow))
                .OrderByDescending(n => n.VfrImpact)
                .ThenBy(n => n.EffectiveFrom)
                .ToListAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "NOTAM fetch/store hatası: {ICAO}", icao);

            // Hata durumunda DB'deki son veriyi dön
            return await _db.Notams
                .Where(n => n.AirportIdent == icao
                         && (n.EffectiveTo == null || n.EffectiveTo > DateTime.UtcNow))
                .OrderByDescending(n => n.VfrImpact)
                .ThenBy(n => n.EffectiveFrom)
                .ToListAsync(ct);
        }
    }

    private static NotamDto ToDto(Notam n) => new(
        n.NotamId,
        n.AirportIdent,
        n.Subject,
        n.Traffic.ToString(),
        n.Scope.ToString(),
        n.VfrImpact.ToString(),
        n.EffectiveFrom,
        n.EffectiveTo,
        n.IsPermanent,
        n.Schedule,
        n.LowerLimit,
        n.UpperLimit,
        n.RawText,
        n.SourceProvider
    );
}

// ── DTO'lar ───────────────────────────────────────────────────────────────────

public record NotamDto(
    string NotamId,
    string AirportIdent,
    string Subject,
    string Traffic,
    string Scope,
    string VfrImpact,
    DateTime EffectiveFrom,
    DateTime? EffectiveTo,
    bool IsPermanent,
    string? Schedule,
    string LowerLimit,
    string UpperLimit,
    string RawText,
    string SourceProvider
);

public record NotamSummaryDto(
    string AirportIdent,
    int ActiveCount,
    bool HasOperationsCritical,
    bool HasVfrWarning,
    bool HasVfrCaution
);
