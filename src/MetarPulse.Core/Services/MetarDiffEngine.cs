using MetarPulse.Core.Enums;
using MetarPulse.Core.Models;

namespace MetarPulse.Core.Services;

/// <summary>
/// İki METAR arasındaki değişimleri karşılaştırır ve MetarComparison üretir.
/// Bildirim motoru tarafından "kötüleşme / iyileşme" tespiti için kullanılır.
/// </summary>
public static class MetarDiffEngine
{
    // Rüzgar hızı eşik değerleri (knot)
    private const int WindSpeedDeltaThreshold = 5;
    private const int WindDirectionDeltaThreshold = 30;
    private const int GustThreshold = 15;

    // Görüş eşik değerleri (metre)
    private const int VisibilityDeltaThreshold = 1000;

    // Tavan eşik değerleri (feet)
    private const int CeilingDeltaThreshold = 500;

    public static MetarComparison Compare(Metar oldMetar, Metar newMetar)
    {
        var diff = new MetarComparison
        {
            OldMetar = oldMetar,
            NewMetar = newMetar,
            OldCategory = oldMetar.Category,
            NewCategory = newMetar.Category
        };

        diff.CategoryChanged = oldMetar.Category != newMetar.Category;
        diff.WindChanged = IsWindChanged(oldMetar, newMetar);
        diff.VisibilityChanged = Math.Abs(oldMetar.VisibilityMeters - newMetar.VisibilityMeters) >= VisibilityDeltaThreshold;
        diff.CeilingChanged = Math.Abs(oldMetar.CeilingFeet - newMetar.CeilingFeet) >= CeilingDeltaThreshold;
        diff.SignificantWeatherChanged = IsSignificantWeatherChanged(oldMetar, newMetar);

        // İyileşme / kötüleşme (düşük enum değeri = daha kötü)
        if (diff.CategoryChanged)
        {
            diff.IsImproving = (int)newMetar.Category > (int)oldMetar.Category;
            diff.IsDeteriorating = (int)newMetar.Category < (int)oldMetar.Category;
        }
        else
        {
            diff.IsImproving = !diff.IsDeteriorating && IsConditionsImproving(oldMetar, newMetar);
            diff.IsDeteriorating = IsConditionsDeteriorating(oldMetar, newMetar);
        }

        diff.ChangeSummary = BuildSummary(diff, oldMetar, newMetar);
        return diff;
    }

    // ── Değişim kontrolleri ──────────────────────────────────────────────────

    private static bool IsWindChanged(Metar o, Metar n)
    {
        if (Math.Abs(o.WindSpeed - n.WindSpeed) >= WindSpeedDeltaThreshold)
            return true;

        if (!o.IsVariableWind && !n.IsVariableWind)
        {
            var dirDelta = Math.Abs(o.WindDirection - n.WindDirection);
            if (dirDelta > 180) dirDelta = 360 - dirDelta;
            if (dirDelta >= WindDirectionDeltaThreshold)
                return true;
        }

        // Gusts
        bool hadGust = o.WindGust.HasValue && o.WindGust >= GustThreshold;
        bool hasGust = n.WindGust.HasValue && n.WindGust >= GustThreshold;
        return hadGust != hasGust;
    }

    private static bool IsSignificantWeatherChanged(Metar o, Metar n)
    {
        var oldSig = GetSignificantWeatherCodes(o);
        var newSig = GetSignificantWeatherCodes(n);

        if (oldSig.Count != newSig.Count) return true;
        return oldSig.Except(newSig).Any() || newSig.Except(oldSig).Any();
    }

    /// <summary>
    /// TS bir descriptor olduğu için, önemli hava durumu tespiti
    /// hem phenomena hem descriptor kontrolü yapar.
    /// </summary>
    private static HashSet<string> GetSignificantWeatherCodes(Metar m)
    {
        var significantPhenomena = new HashSet<WeatherPhenomenon>
        {
            WeatherPhenomenon.FG, WeatherPhenomenon.SN,
            WeatherPhenomenon.GR, WeatherPhenomenon.FC,
            WeatherPhenomenon.SS, WeatherPhenomenon.DS, WeatherPhenomenon.VA
        };

        var codes = new HashSet<string>();
        foreach (var wc in m.WeatherConditions)
        {
            if (wc.Descriptor is WeatherDescriptor.TS or WeatherDescriptor.FZ)
                codes.Add(wc.Descriptor.ToString());
            foreach (var p in wc.Phenomena.Where(p => significantPhenomena.Contains(p)))
                codes.Add(p.ToString());
        }
        return codes;
    }

    private static bool IsConditionsImproving(Metar o, Metar n)
        => n.VisibilityMeters > o.VisibilityMeters + VisibilityDeltaThreshold
        || n.CeilingFeet > o.CeilingFeet + CeilingDeltaThreshold;

    private static bool IsConditionsDeteriorating(Metar o, Metar n)
        => n.VisibilityMeters < o.VisibilityMeters - VisibilityDeltaThreshold
        || n.CeilingFeet < o.CeilingFeet - CeilingDeltaThreshold;

    // ── Özet metin üretimi ───────────────────────────────────────────────────

    private static List<string> BuildSummary(MetarComparison diff, Metar o, Metar n)
    {
        var summary = new List<string>();

        if (diff.CategoryChanged)
            summary.Add($"Uçuş kategorisi: {o.Category} → {n.Category}");

        if (diff.VisibilityChanged)
            summary.Add($"Görüş: {FormatVis(o.VisibilityMeters)} → {FormatVis(n.VisibilityMeters)}");

        if (diff.CeilingChanged)
            summary.Add($"Tavan: {FormatCeiling(o.CeilingFeet)} → {FormatCeiling(n.CeilingFeet)} ft");

        if (diff.WindChanged)
            summary.Add($"Rüzgar: {FormatWind(o)} → {FormatWind(n)}");

        if (diff.SignificantWeatherChanged)
        {
            var oldWx = string.Join(" ", o.WeatherConditions.Select(w => w.ToString()));
            var newWx = string.Join(" ", n.WeatherConditions.Select(w => w.ToString()));
            var oldStr = string.IsNullOrEmpty(oldWx) ? "yok" : oldWx;
            var newStr = string.IsNullOrEmpty(newWx) ? "yok" : newWx;
            summary.Add($"Hava durumu: {oldStr} → {newStr}");
        }

        return summary;
    }

    private static string FormatVis(int meters)
        => meters >= 10000 ? "10km+" : $"{meters}m";

    private static string FormatCeiling(int feet)
        => feet >= 99999 ? "SKC" : $"{feet}";

    private static string FormatWind(Metar m)
    {
        var dir = m.IsVariableWind ? "VRB" : $"{m.WindDirection:D3}°";
        var gust = m.WindGust.HasValue ? $"G{m.WindGust}kt" : "";
        return $"{dir}/{m.WindSpeed}kt{gust}";
    }
}
