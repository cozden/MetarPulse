using MetarPulse.Abstractions.Repositories;
using MetarPulse.Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace MetarPulse.Api.Controllers;

[ApiController]
[Route("api/runway")]
public class RunwayController : ControllerBase
{
    private readonly IAirportRepository _airportRepo;
    private readonly IMetarRepository _metarRepo;

    public RunwayController(IAirportRepository airportRepo, IMetarRepository metarRepo)
    {
        _airportRepo = airportRepo;
        _metarRepo = metarRepo;
    }

    /// <summary>
    /// GET /api/runway/{icao}/wind — Güncel METAR'a göre tüm pistler için rüzgar analizi.
    /// Sonuçlar crosswind bileşenine göre sıralanır (en iyi pist önce).
    /// </summary>
    [HttpGet("{icao}/wind")]
    public async Task<IActionResult> GetRunwayWind(string icao, CancellationToken ct)
    {
        icao = icao.ToUpperInvariant();

        var airport = await _airportRepo.GetWithRunwaysAsync(icao, ct);
        if (airport is null)
            return NotFound(new { message = $"Meydan bulunamadı: {icao}" });

        var metar = await _metarRepo.GetLatestAsync(icao, ct);
        if (metar is null)
            return NotFound(new { message = $"Güncel METAR bulunamadı: {icao}" });

        if (airport.Runways.Count == 0)
            return Ok(new RunwayWindResponse(icao, metar.WindDirection, metar.WindSpeed, metar.WindGust, []));

        var analyses = new List<RunwayWindDto>();

        foreach (var rwy in airport.Runways.Where(r => !r.IsClosed))
        {
            // Low end — pist kimliğinden magnetic heading türet ("34L" → 340°)
            if (rwy.LeIdent is not null)
            {
                var magHeading = RunwayIdentToMagneticHeading(rwy.LeIdent)
                    ?? (rwy.LeHeadingDegT.HasValue
                        ? WindCalculator.TrueToMagnetic(rwy.LeHeadingDegT.Value, airport.MagneticVariation ?? 0)
                        : (double?)null);
                if (magHeading is null) continue;

                var wc = WindCalculator.Calculate(rwy.LeIdent, magHeading.Value, metar.WindDirection, metar.WindSpeed);
                analyses.Add(new RunwayWindDto(rwy.LeIdent, (int)Math.Round(magHeading.Value),
                    Math.Round(wc.HeadwindKnots, 1), Math.Round(wc.CrosswindKnots, 1),
                    Math.Round(wc.TailwindKnots, 1), wc.IsTailwind, rwy.LengthFt));
            }

            // High end
            if (rwy.HeIdent is not null)
            {
                var magHeading = RunwayIdentToMagneticHeading(rwy.HeIdent)
                    ?? (rwy.HeHeadingDegT.HasValue
                        ? WindCalculator.TrueToMagnetic(rwy.HeHeadingDegT.Value, airport.MagneticVariation ?? 0)
                        : (double?)null);
                if (magHeading is null) continue;

                var wc = WindCalculator.Calculate(rwy.HeIdent, magHeading.Value, metar.WindDirection, metar.WindSpeed);
                analyses.Add(new RunwayWindDto(rwy.HeIdent, (int)Math.Round(magHeading.Value),
                    Math.Round(wc.HeadwindKnots, 1), Math.Round(wc.CrosswindKnots, 1),
                    Math.Round(wc.TailwindKnots, 1), wc.IsTailwind, rwy.LengthFt));
            }
        }

        // Crosswind en düşük = en iyi pist (tailwind'li pistleri sona al)
        var sorted = analyses
            .OrderBy(a => a.IsTailwind)
            .ThenBy(a => a.CrosswindKnots)
            .ToList();

        return Ok(new RunwayWindResponse(
            icao,
            metar.WindDirection,
            metar.WindSpeed,
            metar.WindGust,
            sorted));
    }

    /// <summary>"34L" → 340, "09R" → 90, "36" → 360. Geçersizse null.</summary>
    private static double? RunwayIdentToMagneticHeading(string ident)
    {
        var digits = new string(ident.TakeWhile(char.IsDigit).ToArray());
        if (int.TryParse(digits, out var num) && num >= 1 && num <= 36)
            return num * 10.0;
        return null;
    }
}

// ── DTO'lar ───────────────────────────────────────────────────────────────────

public record RunwayWindResponse(
    string StationId,
    int WindDirection,
    int WindSpeed,
    int? WindGust,
    List<RunwayWindDto> Runways
);

public record RunwayWindDto(
    string RunwayIdent,
    int HeadingDeg,
    double HeadwindKnots,
    double CrosswindKnots,
    double TailwindKnots,
    bool IsTailwind,
    int? LengthFt
);
