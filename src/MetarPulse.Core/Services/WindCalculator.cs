using MetarPulse.Core.Models;

namespace MetarPulse.Core.Services;

/// <summary>
/// Pist yönüne göre headwind/crosswind/tailwind bileşenlerini hesaplar.
/// </summary>
public static class WindCalculator
{
    /// <param name="runwayHeadingMagnetic">Pist manyetik yönü (derece)</param>
    /// <param name="windDirectionMagnetic">METAR rüzgar yönü (derece, magnetic)</param>
    /// <param name="windSpeedKnots">METAR rüzgar hızı (knot)</param>
    public static WindComponent Calculate(
        string runwayIdent,
        double runwayHeadingMagnetic,
        int windDirectionMagnetic,
        int windSpeedKnots)
    {
        var angleDiffRad = (windDirectionMagnetic - runwayHeadingMagnetic) * Math.PI / 180.0;

        var headwind = windSpeedKnots * Math.Cos(angleDiffRad);
        var crosswind = windSpeedKnots * Math.Sin(angleDiffRad);

        return new WindComponent
        {
            RunwayIdent = runwayIdent,
            RunwayHeadingMagnetic = runwayHeadingMagnetic,
            HeadwindKnots = headwind > 0 ? Math.Round(headwind, 1) : 0,
            TailwindKnots = headwind < 0 ? Math.Round(Math.Abs(headwind), 1) : 0,
            CrosswindKnots = Math.Round(Math.Abs(crosswind), 1)
        };
    }

    /// <summary>Magnetic variation uygulayarak True heading'den Magnetic heading hesaplar.</summary>
    public static double TrueToMagnetic(double trueHeading, double magneticVariation)
        => (trueHeading - magneticVariation + 360) % 360;
}
