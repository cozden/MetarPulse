using System.Security.Claims;
using MetarPulse.Abstractions.Repositories;
using MetarPulse.Core.Models;
using MetarPulse.Infrastructure.Persistence.PostgreSQL;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MetarPulse.Api.Controllers;

[ApiController]
[Route("api/bookmarks")]
[Authorize]
public class BookmarkController : ControllerBase
{
    private readonly IBookmarkRepository _bookmarks;
    private readonly IAirportRepository _airports;
    private readonly AppDbContext _db;

    public BookmarkController(
        IBookmarkRepository bookmarks,
        IAirportRepository airports,
        AppDbContext db)
    {
        _bookmarks = bookmarks;
        _airports = airports;
        _db = db;
    }

    /// <summary>GET /api/bookmarks — Kullanıcının favorileri (meydan bilgisi dahil)</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var userId = GetUserId();
        var items = await _bookmarks.GetByUserIdAsync(userId, ct);

        return Ok(items.Select(b => new
        {
            b.Id,
            b.StationIcao,
            b.SortOrder,
            b.CreatedAt,
            Airport = b.Airport == null ? null : new
            {
                b.Airport.Ident,
                b.Airport.Name,
                b.Airport.Type,
                b.Airport.IsoCountry,
                b.Airport.Municipality,
                b.Airport.IataCode,
                b.Airport.LatitudeDeg,
                b.Airport.LongitudeDeg,
                b.Airport.ElevationFt
            }
        }));
    }

    /// <summary>POST /api/bookmarks — Yeni favori ekle</summary>
    [HttpPost]
    public async Task<IActionResult> Add([FromBody] AddBookmarkRequest req, CancellationToken ct)
    {
        var userId = GetUserId();
        var icao = req.IcaoCode.ToUpperInvariant();

        // Meydan var mı?
        var airport = await _airports.GetByIcaoAsync(icao, ct);
        if (airport == null)
            return NotFound(new { message = $"Meydan bulunamadı: {icao}" });

        // Zaten eklenmiş mi?
        if (await _bookmarks.ExistsAsync(userId, icao, ct))
            return Conflict(new { message = $"{icao} zaten favorilerinizde." });

        // Sıra numarası — mevcut en yüksek + 1
        var existing = await _bookmarks.GetByUserIdAsync(userId, ct);
        var sortOrder = existing.Count > 0 ? existing.Max(b => b.SortOrder) + 1 : 0;

        var bookmark = new UserBookmark
        {
            UserId = userId,
            StationIcao = icao,
            SortOrder = sortOrder,
            CreatedAt = DateTime.UtcNow
        };

        await _bookmarks.AddAsync(bookmark, ct);
        await _db.SaveChangesAsync(ct);

        return Created($"/api/bookmarks", new
        {
            bookmark.Id,
            bookmark.StationIcao,
            bookmark.SortOrder,
            bookmark.CreatedAt
        });
    }

    /// <summary>DELETE /api/bookmarks/{icao} — Favoriyi kaldır</summary>
    [HttpDelete("{icao}")]
    public async Task<IActionResult> Remove(string icao, CancellationToken ct)
    {
        var userId = GetUserId();
        var bookmark = await _bookmarks.GetByUserAndStationAsync(userId, icao.ToUpperInvariant(), ct);

        if (bookmark == null)
            return NotFound(new { message = $"{icao.ToUpperInvariant()} favorilerinizde değil." });

        _bookmarks.Remove(bookmark);
        await _db.SaveChangesAsync(ct);

        return NoContent();
    }

    /// <summary>PUT /api/bookmarks/reorder — Favori sırasını güncelle</summary>
    [HttpPut("reorder")]
    public async Task<IActionResult> Reorder([FromBody] ReorderRequest req, CancellationToken ct)
    {
        var userId = GetUserId();
        var existing = await _bookmarks.GetByUserIdAsync(userId, ct);

        foreach (var item in req.Items)
        {
            var bookmark = existing.FirstOrDefault(b =>
                b.StationIcao.Equals(item.IcaoCode, StringComparison.OrdinalIgnoreCase));
            if (bookmark != null)
                bookmark.SortOrder = item.SortOrder;
        }

        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>GET /api/bookmarks/icaos — Yalnızca ICAO kodu listesi (dropdown için)</summary>
    [HttpGet("icaos")]
    public async Task<IActionResult> GetIcaos(CancellationToken ct)
    {
        var userId = GetUserId();
        var icaos = await _bookmarks.GetStationIcaosAsync(userId, ct);
        return Ok(icaos);
    }

    // ── Yardımcı ─────────────────────────────────────────────────────────────

    private string GetUserId()
        => User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.FindFirstValue("sub")
        ?? throw new UnauthorizedAccessException("Kullanıcı kimliği bulunamadı.");
}

// ── Request kayıtları ─────────────────────────────────────────────────────────

public record AddBookmarkRequest(string IcaoCode);

public record ReorderRequest(List<ReorderItem> Items);

public record ReorderItem(string IcaoCode, int SortOrder);
