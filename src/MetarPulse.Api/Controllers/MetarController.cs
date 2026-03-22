using MetarPulse.Abstractions.Providers;
using MetarPulse.Abstractions.Repositories;
using MetarPulse.Api.Hubs;
using MetarPulse.Core.Services;
using MetarPulse.Infrastructure.Persistence.PostgreSQL;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Caching.Memory;

namespace MetarPulse.Api.Controllers;

[ApiController]
[Route("api/metar")]
public class MetarController : ControllerBase
{
    private readonly IMetarRepository _metarRepo;
    private readonly IProviderManager _provider;
    private readonly IHubContext<MetarHub> _hub;
    private readonly ILogger<MetarController> _logger;
    private readonly AppDbContext _db;
    private readonly IMemoryCache _cache;

    // METAR 10 dakikadan eski ise bayat sayılır
    private static readonly TimeSpan StalenessThreshold = TimeSpan.FromMinutes(10);

    // TAF 30 dakika cache'lenir (TAF saatlerce geçerli)
    private static readonly TimeSpan TafCacheDuration = TimeSpan.FromMinutes(30);

    public MetarController(
        IMetarRepository metarRepo,
        IProviderManager provider,
        IHubContext<MetarHub> hub,
        ILogger<MetarController> logger,
        AppDbContext db,
        IMemoryCache cache)
    {
        _metarRepo = metarRepo;
        _provider = provider;
        _hub = hub;
        _logger = logger;
        _db = db;
        _cache = cache;
    }

    /// <summary>GET /api/metar/{icao} — Güncel METAR (önce cache, bayatsa provider'dan)</summary>
    [HttpGet("{icao}")]
    public async Task<IActionResult> GetMetar(string icao, CancellationToken ct)
    {
        icao = icao.ToUpperInvariant();

        var cached = await _metarRepo.GetLatestAsync(icao, ct);
        var isStale = cached == null
                   || cached.FetchedAt < DateTime.UtcNow - StalenessThreshold;

        if (!isStale && cached != null)
            return Ok(ToDto(cached));

        // Cache bayat veya yok — provider'dan çek
        var fresh = await _provider.GetMetarWithFallbackAsync(icao, ct);
        if (fresh != null)
        {
            if (cached != null && cached.ObservationTime == fresh.ObservationTime)
                cached.FetchedAt = fresh.FetchedAt;
            else
                await _metarRepo.AddAsync(fresh, ct);
            await _db.SaveChangesAsync(ct);

            // SignalR push
            await _hub.Clients
                .Group(MetarHub.StationGroup(icao))
                .SendAsync("ReceiveMetar", ToDto(fresh), ct);

            return Ok(ToDto(fresh));
        }

        // Provider'dan gelmediyse bayat cache dön
        if (cached != null)
        {
            _logger.LogWarning("Stale METAR döndürülüyor: {ICAO}", icao);
            return Ok(ToDto(cached) with { IsStale = true });
        }

        return NotFound(new { message = $"METAR bulunamadı: {icao}" });
    }

    /// <summary>GET /api/metar/{icao}/history?hours=24 — METAR geçmişi</summary>
    [HttpGet("{icao}/history")]
    public async Task<IActionResult> GetHistory(
        string icao,
        [FromQuery] int hours = 24,
        CancellationToken ct = default)
    {
        icao = icao.ToUpperInvariant();
        hours = Math.Clamp(hours, 1, 168); // Max 7 gün

        var history = await _metarRepo.GetHistoryAsync(icao, hours, ct);

        if (history.Count == 0)
            return NotFound(new { message = $"{icao} için son {hours} saatte METAR kaydı yok." });

        return Ok(history.Select(ToDto));
    }

    /// <summary>GET /api/metar/{icao}/taf — TAF tahmini (30 dk cache)</summary>
    [HttpGet("{icao}/taf")]
    public async Task<IActionResult> GetTaf(string icao, CancellationToken ct)
    {
        icao = icao.ToUpperInvariant();
        var cacheKey = $"taf:{icao}";

        if (_cache.TryGetValue(cacheKey, out object? cached) && cached is not null)
            return Ok(cached);

        var taf = await _provider.GetTafWithFallbackAsync(icao, ct);

        if (taf == null)
            return NotFound(new { message = $"TAF bulunamadı: {icao}" });

        var payload = new
        {
            taf.StationId,
            taf.RawText,
            IssueTime = taf.IssueTime.ToString("O"),
            ValidFrom = taf.ValidFrom.ToString("O"),
            ValidTo = taf.ValidTo.ToString("O"),
            taf.SourceProvider,
            Periods = taf.Periods.Select(p => new
            {
                From = p.From.ToString("O"),
                To = p.To.ToString("O"),
                p.ChangeIndicator,
                p.Probability,
                p.WindDirection,
                p.WindSpeed,
                p.WindGust,
                p.VisibilityMeters,
                CloudLayers = p.CloudLayers.Select(c => new
                {
                    Coverage = c.Coverage.ToString(),
                    c.AltitudeFt,
                    Type = c.Type.ToString()
                }),
                WeatherConditions = p.WeatherConditions.Select(w => w.ToString())
            }).ToList()
        };

        _cache.Set(cacheKey, (object)payload, TafCacheDuration);
        return Ok(payload);
    }

    /// <summary>POST /api/metar/{icao}/refresh — Zorla güncelle ve SignalR ile push yap</summary>
    [HttpPost("{icao}/refresh")]
    public async Task<IActionResult> Refresh(string icao, CancellationToken ct)
    {
        icao = icao.ToUpperInvariant();
        _logger.LogInformation("Manuel refresh: {ICAO}", icao);

        var previous = await _metarRepo.GetLatestAsync(icao, ct);
        var fresh = await _provider.GetMetarWithFallbackAsync(icao, ct);

        if (fresh == null)
            return StatusCode(503, new { message = $"Provider'dan veri alınamadı: {icao}" });

        if (previous != null && previous.ObservationTime == fresh.ObservationTime)
            previous.FetchedAt = fresh.FetchedAt;
        else
            await _metarRepo.AddAsync(fresh, ct);
        await _db.SaveChangesAsync(ct);

        MetarPulse.Core.Models.MetarComparison? diff = null;
        if (previous != null)
            diff = MetarDiffEngine.Compare(previous, fresh);

        // SignalR push
        await _hub.Clients
            .Group(MetarHub.StationGroup(icao))
            .SendAsync("ReceiveMetar", ToDto(fresh), ct);

        return Ok(new
        {
            Metar = ToDto(fresh),
            Changed = diff != null,
            diff?.CategoryChanged,
            diff?.IsImproving,
            diff?.IsDeteriorating,
            ChangeSummary = diff?.ChangeSummary ?? []
        });
    }

    /// <summary>GET /api/metar/bulk?icao=LTFM,EGLL,KJFK — Birden fazla istasyon (bayat olanlar provider'dan güncellenir)</summary>
    [HttpGet("bulk")]
    public async Task<IActionResult> GetBulk(
        [FromQuery] string icao,
        CancellationToken ct)
    {
        var codes = icao.Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(c => c.Trim().ToUpperInvariant())
                        .Take(20)
                        .ToList();

        if (codes.Count == 0)
            return BadRequest(new { message = "En az bir ICAO kodu gerekli." });

        var cached  = await _metarRepo.GetLatestForStationsAsync(codes, ct);
        var byIcao  = cached.ToDictionary(m => m.StationId);
        var now     = DateTime.UtcNow;

        // Bayat veya eksik istasyonları belirle
        var staleCodes = codes
            .Where(c => !byIcao.TryGetValue(c, out var m)
                        || m.FetchedAt < now - StalenessThreshold)
            .ToList();

        if (staleCodes.Count > 0)
        {
            // Provider çağrılarını paralel yap (hızlı)
            var fetchTasks = staleCodes
                .Select(c => _provider.GetMetarWithFallbackAsync(c, ct))
                .ToList();
            var fetchedArr = await Task.WhenAll(fetchTasks);

            // DB yazımı sıralı (DbContext thread-safe değil)
            for (int i = 0; i < staleCodes.Count; i++)
            {
                if (fetchedArr[i] is { } fresh)
                {
                    // Aynı gözlem zamanı zaten varsa sadece FetchedAt güncelle (duplicate key önle)
                    if (byIcao.TryGetValue(staleCodes[i], out var existing) &&
                        existing.ObservationTime == fresh.ObservationTime)
                    {
                        existing.FetchedAt = fresh.FetchedAt;
                    }
                    else
                    {
                        await _metarRepo.AddAsync(fresh, ct);
                    }
                    byIcao[staleCodes[i]] = fresh;
                }
            }

            await _db.SaveChangesAsync(ct);
        }

        var results = codes
            .Where(byIcao.ContainsKey)
            .Select(c => byIcao[c]);

        return Ok(results.Select(ToDto));
    }

    // ── DTO dönüşümü ─────────────────────────────────────────────────────────

    private static MetarDto ToDto(MetarPulse.Core.Models.Metar m) => new(
        m.StationId,
        m.RawText,
        m.ObservationTime,
        m.WindDirection,
        m.WindSpeed,
        m.WindGust,
        m.IsVariableWind,
        m.VariableWindFrom,
        m.VariableWindTo,
        m.VisibilityMeters,
        m.IsCavok,
        m.CeilingFeet,
        m.Category.ToString(),
        m.Temperature,
        m.DewPoint,
        m.AltimeterHpa,
        m.AltimeterInHg,
        m.Trend,
        m.IsSpeci,
        m.SourceProvider,
        m.FetchedAt,
        m.CloudLayers.Select(c => new CloudLayerDto(c.Coverage.ToString(), c.AltitudeFt, c.Type.ToString())).ToList(),
        m.WeatherConditions.Select(w => w.ToString()).ToList()
    );
}

// ── DTO kayıtları ─────────────────────────────────────────────────────────────

public record MetarDto(
    string StationId,
    string RawText,
    DateTime ObservationTime,
    int WindDirection,
    int WindSpeed,
    int? WindGust,
    bool IsVariableWind,
    int? VariableWindFrom,
    int? VariableWindTo,
    int VisibilityMeters,
    bool IsCavok,
    int CeilingFeet,
    string Category,
    int? Temperature,
    int? DewPoint,
    decimal? AltimeterHpa,
    decimal? AltimeterInHg,
    string? Trend,
    bool IsSpeci,
    string? SourceProvider,
    DateTime FetchedAt,
    List<CloudLayerDto> CloudLayers,
    List<string> WeatherConditions,
    bool IsStale = false
);

public record CloudLayerDto(string Coverage, int AltitudeFt, string Type);
