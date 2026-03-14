using MetarPulse.Core.Enums;
using MetarPulse.Core.Models;

namespace MetarPulse.Core.Services;

/// <summary>
/// METAR verilerinden VFR/MVFR/IFR/LIFR kategorisini belirler.
/// İki kriterden (görüş ve tavan) kötü olan geçerlidir.
/// </summary>
public static class FlightCategoryResolver
{
    public static FlightCategory Resolve(int visibilityMeters, int ceilingFeet)
    {
        var fromVisibility = ResolveFromVisibility(visibilityMeters);
        var fromCeiling = ResolveFromCeiling(ceilingFeet);

        // İkisinden daha kötü olanı döndür (düşük enum değeri = daha kötü)
        return (FlightCategory)Math.Min((int)fromVisibility, (int)fromCeiling);
    }

    public static FlightCategory ResolveFromMetar(Metar metar)
    {
        var ceiling = CalculateCeiling(metar.CloudLayers);
        return Resolve(metar.VisibilityMeters, ceiling);
    }

    public static int CalculateCeiling(List<CloudLayer> layers)
    {
        // BKN veya OVC katmanlarından en alçak olanı tavan kabul edilir
        var ceilingLayer = layers
            .Where(l => l.Coverage is CloudCoverage.BKN or CloudCoverage.OVC or CloudCoverage.VV)
            .OrderBy(l => l.AltitudeFt)
            .FirstOrDefault();

        return ceilingLayer?.AltitudeFt ?? 99999; // Tavan yoksa unlimited
    }

    private static FlightCategory ResolveFromVisibility(int meters)
    {
        // SM → metre dönüşüm: 1SM ≈ 1609m, 3SM ≈ 4828m, 5SM ≈ 8046m
        return meters switch
        {
            < 1609 => FlightCategory.LIFR,  // < 1SM
            < 4828 => FlightCategory.IFR,   // 1-3SM
            < 8046 => FlightCategory.MVFR,  // 3-5SM
            _ => FlightCategory.VFR          // > 5SM
        };
    }

    private static FlightCategory ResolveFromCeiling(int feet)
    {
        return feet switch
        {
            < 500 => FlightCategory.LIFR,
            < 1000 => FlightCategory.IFR,
            < 3000 => FlightCategory.MVFR,
            _ => FlightCategory.VFR
        };
    }
}
