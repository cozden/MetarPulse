using MetarPulse.Abstractions.Providers;
using MetarPulse.Abstractions.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace MetarPulse.Api.Controllers;

[ApiController]
[Route("api/airport")]
public class AirportController : ControllerBase
{
    private readonly IAirportRepository _airports;
    private readonly IAirportDataProvider _provider;
    private readonly ILogger<AirportController> _logger;

    public AirportController(
        IAirportRepository airports,
        IAirportDataProvider provider,
        ILogger<AirportController> logger)
    {
        _airports = airports;
        _provider = provider;
        _logger = logger;
    }

    /// <summary>GET /api/airport/{icao} — Meydan detayı (pistler dahil)</summary>
    [HttpGet("{icao}")]
    public async Task<IActionResult> GetAirport(string icao, CancellationToken ct)
    {
        var airport = await _airports.GetWithRunwaysAsync(icao.ToUpper(), ct);
        if (airport == null)
            return NotFound(new { message = $"Meydan bulunamadı: {icao.ToUpper()}" });

        return Ok(new
        {
            airport.Ident,
            airport.Name,
            airport.Type,
            airport.LatitudeDeg,
            airport.LongitudeDeg,
            airport.ElevationFt,
            airport.IsoCountry,
            airport.Municipality,
            airport.IataCode,
            Runways = airport.Runways.Select(r => new
            {
                r.LeIdent,
                r.HeIdent,
                r.LengthFt,
                r.WidthFt,
                r.Surface,
                r.IsLighted,
                r.IsClosed,
                r.LeHeadingDegT,
                r.HeHeadingDegT
            })
        });
    }

    /// <summary>GET /api/airport/search?q=LTFM — ICAO/isim/IATA ile arama</summary>
    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string q, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Length < 2)
            return BadRequest(new { message = "En az 2 karakter giriniz." });

        var results = await _airports.SearchAsync(q, limit: 20, ct);

        return Ok(results.Select(a => new
        {
            a.Ident,
            a.Name,
            a.Type,
            a.IsoCountry,
            a.Municipality,
            a.IataCode,
            a.LatitudeDeg,
            a.LongitudeDeg
        }));
    }

    /// <summary>POST /api/airport/sync — Manuel meydan DB güncelleme (Admin)</summary>
    [HttpPost("sync")]
    public async Task<IActionResult> Sync(CancellationToken ct)
    {
        _logger.LogInformation("Manuel airport sync tetiklendi.");
        var result = await _provider.SyncAllAsync(ct);
        return Ok(new
        {
            result.Added,
            result.Updated,
            result.Errors,
            result.ErrorMessage
        });
    }
}
