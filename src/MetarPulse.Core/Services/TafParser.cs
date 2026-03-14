using System.Text.RegularExpressions;
using MetarPulse.Core.Enums;
using MetarPulse.Core.Models;

namespace MetarPulse.Core.Services;

/// <summary>
/// Ham TAF stringini Taf modeline dönüştürür.
/// BECMG / TEMPO / FM / PROB30 / PROB40 period'larını destekler.
/// </summary>
public static class TafParser
{
    // TAF LTFM 121100Z 1212/1318 ...
    private static readonly Regex IssueTimeRx = new(
        @"^(\d{2})(\d{2})(\d{2})Z$", RegexOptions.Compiled);

    // 1212/1318  →  from=12th 12:00, to=13th 18:00
    private static readonly Regex PeriodRx = new(
        @"^(\d{2})(\d{2})/(\d{2})(\d{2})$", RegexOptions.Compiled);

    // FM121500  →  12th 15:00
    private static readonly Regex FmRx = new(
        @"^FM(\d{2})(\d{2})(\d{2})$", RegexOptions.Compiled);

    private static readonly Regex WindRx = new(
        @"^(VRB|\d{3})(\d{2,3})(G(\d{2,3}))?(KT|MPS)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex VisMRx = new(@"^(\d{4})$", RegexOptions.Compiled);
    private static readonly Regex VisSmRx = new(@"^(\d+(?:/\d+)?)SM$", RegexOptions.Compiled);

    private static readonly Regex CloudRx = new(
        @"^(FEW|SCT|BKN|OVC|VV)(\d{3}|///)(CB|TCU)?$|^(SKC|CLR|NSC|NCD)$",
        RegexOptions.Compiled);

    private static readonly Regex WxRx = new(
        @"^[-+]?(VC)?((MI|BC|PR|DR|BL|SH|TS|FZ)|(RA|SN|DZ|SG|IC|PL|GR|GS|FG|BR|HZ|FU|VA|DU|SA|PY|SQ|FC|SS|DS)){1,}$",
        RegexOptions.Compiled);

    // Change group başlangıcını tespit et
    private static readonly HashSet<string> ChangeKeywords =
        new(StringComparer.OrdinalIgnoreCase)
        { "BECMG", "TEMPO", "FM", "PROB30", "PROB40" };

    public static Taf Parse(string rawText, string? sourceProvider = null)
    {
        var taf = new Taf
        {
            RawText = rawText.Trim(),
            SourceProvider = sourceProvider,
            FetchedAt = DateTime.UtcNow
        };

        var tokens = taf.RawText.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        int i = 0;

        // 1. Tip: TAF [AMD|COR]
        if (i < tokens.Length && tokens[i].Equals("TAF", StringComparison.OrdinalIgnoreCase)) i++;
        if (i < tokens.Length && tokens[i] is "AMD" or "COR" or "CORR") i++;

        // 2. İstasyon
        if (i < tokens.Length && tokens[i].Length == 4)
        {
            taf.StationId = tokens[i].ToUpper();
            i++;
        }

        // 3. Yayın zamanı: DDHHMMz
        if (i < tokens.Length)
        {
            var m = IssueTimeRx.Match(tokens[i]);
            if (m.Success)
            {
                taf.IssueTime = BuildUtcTime(
                    int.Parse(m.Groups[1].Value),
                    int.Parse(m.Groups[2].Value),
                    int.Parse(m.Groups[3].Value));
                i++;
            }
        }

        // 4. Geçerlilik dönemi: 1212/1318
        if (i < tokens.Length)
        {
            var m = PeriodRx.Match(tokens[i]);
            if (m.Success)
            {
                taf.ValidFrom = BuildPeriodTime(
                    int.Parse(m.Groups[1].Value),
                    int.Parse(m.Groups[2].Value), taf.IssueTime);
                taf.ValidTo = BuildPeriodTime(
                    int.Parse(m.Groups[3].Value),
                    int.Parse(m.Groups[4].Value), taf.IssueTime);
                i++;
            }
        }

        // 5. Ana period ve değişim gruplarını tokenlar halinde ayır
        var segments = SplitIntoSegments(tokens, i);

        // 6. Ana period (baz tahmin)
        if (segments.Count > 0)
        {
            var basePeriod = ParsePeriodTokens(segments[0], "BASE", null, taf.ValidFrom, taf.ValidTo, taf.IssueTime);
            taf.Periods.Add(basePeriod);
        }

        // 7. Değişim grupları
        for (int s = 1; s < segments.Count; s++)
        {
            var seg = segments[s];
            if (seg.Count == 0) continue;

            var keyword = seg[0].ToUpper();

            if (keyword.StartsWith("FM"))
            {
                var period = ParseFmSegment(seg, taf.IssueTime, taf.ValidTo);
                if (period != null) taf.Periods.Add(period);
            }
            else if (keyword is "BECMG" or "TEMPO")
            {
                var period = ParsePeriodGroup(seg, keyword, null, taf.IssueTime);
                if (period != null) taf.Periods.Add(period);
            }
            else if (keyword is "PROB30" or "PROB40")
            {
                var prob = int.Parse(keyword[4..]);
                if (seg.Count > 1 && seg[1] is "TEMPO" or "BECMG")
                {
                    // PROB30 TEMPO / PROB40 TEMPO
                    var subKeyword = seg[1].ToUpper();
                    var rest = seg.Skip(1).ToList();
                    var period = ParsePeriodGroup(rest, subKeyword, prob, taf.IssueTime);
                    if (period != null) taf.Periods.Add(period);
                }
                else
                {
                    var period = ParsePeriodGroup(seg.Skip(1).ToList(), "PROB", prob, taf.IssueTime);
                    if (period != null) taf.Periods.Add(period);
                }
            }
        }

        return taf;
    }

    // ── Segment ayırma ────────────────────────────────────────────────────────

    private static List<List<string>> SplitIntoSegments(string[] tokens, int start)
    {
        var segments = new List<List<string>>();
        var current = new List<string>();

        for (int i = start; i < tokens.Length; i++)
        {
            var t = tokens[i].ToUpper();

            bool isChangeKeyword = t is "BECMG" or "TEMPO" or "PROB30" or "PROB40"
                || FmRx.IsMatch(tokens[i]);

            // PROB30/PROB40 grubunun içindeki TEMPO/BECMG ayrı segment başlatmamalı
            bool inProbGroup = current.Count > 0 && current[0].ToUpper() is "PROB30" or "PROB40";
            if (inProbGroup && t is "TEMPO" or "BECMG")
                isChangeKeyword = false;

            if (isChangeKeyword && current.Count > 0)
            {
                segments.Add(current);
                current = new List<string>();
            }
            current.Add(tokens[i]);
        }

        if (current.Count > 0) segments.Add(current);
        return segments;
    }

    // ── Period parse ──────────────────────────────────────────────────────────

    private static TafPeriod ParsePeriodTokens(
        List<string> tokens, string indicator, int? probability,
        DateTime from, DateTime to, DateTime reference)
    {
        var period = new TafPeriod
        {
            ChangeIndicator = indicator,
            Probability = probability,
            From = from,
            To = to
        };

        int i = 0;
        // Skip keyword tokens (BECMG, TEMPO, PROB30, vs.)
        while (i < tokens.Count && (ChangeKeywords.Contains(tokens[i].ToUpper()) || tokens[i].ToUpper() is "BASE"))
            i++;

        // Geçerlilik dönemi varsa atla (BECMG/TEMPO için)
        if (i < tokens.Count && PeriodRx.IsMatch(tokens[i]))
        {
            var m = PeriodRx.Match(tokens[i]);
            period.From = BuildPeriodTime(int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value), reference);
            period.To = BuildPeriodTime(int.Parse(m.Groups[3].Value), int.Parse(m.Groups[4].Value), reference);
            i++;
        }

        ParseConditions(tokens, i, period);
        return period;
    }

    private static TafPeriod? ParsePeriodGroup(
        List<string> tokens, string keyword, int? probability, DateTime reference)
    {
        if (tokens.Count == 0) return null;

        int start = 0;
        if (tokens[0].Equals(keyword, StringComparison.OrdinalIgnoreCase)) start = 1;

        DateTime from = reference, to = reference;

        // Geçerlilik dönemi
        if (start < tokens.Count && PeriodRx.IsMatch(tokens[start]))
        {
            var m = PeriodRx.Match(tokens[start]);
            from = BuildPeriodTime(int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value), reference);
            to   = BuildPeriodTime(int.Parse(m.Groups[3].Value), int.Parse(m.Groups[4].Value), reference);
            start++;
        }

        var period = new TafPeriod
        {
            ChangeIndicator = keyword,
            Probability = probability,
            From = from,
            To = to
        };

        ParseConditions(tokens, start, period);
        return period;
    }

    private static TafPeriod? ParseFmSegment(List<string> tokens, DateTime reference, DateTime tafTo)
    {
        if (tokens.Count == 0) return null;

        var m = FmRx.Match(tokens[0]);
        if (!m.Success) return null;

        var from = BuildPeriodTime(
            int.Parse(m.Groups[1].Value),
            int.Parse(m.Groups[2].Value), reference,
            int.Parse(m.Groups[3].Value));

        var period = new TafPeriod
        {
            ChangeIndicator = "FM",
            From = from,
            To = tafTo
        };

        ParseConditions(tokens, 1, period);
        return period;
    }

    private static void ParseConditions(List<string> tokens, int start, TafPeriod period)
    {
        int i = start;

        // Rüzgar
        if (i < tokens.Count)
        {
            var m = WindRx.Match(tokens[i]);
            if (m.Success)
            {
                bool isMps = m.Groups[5].Value.Equals("MPS", StringComparison.OrdinalIgnoreCase);
                period.WindDirection = m.Groups[1].Value == "VRB"
                    ? 0
                    : int.Parse(m.Groups[1].Value);
                period.WindSpeed = ToKnots(int.Parse(m.Groups[2].Value), isMps);
                if (m.Groups[3].Success)
                    period.WindGust = ToKnots(int.Parse(m.Groups[4].Value), isMps);
                i++;
            }
        }

        // Görüş
        if (i < tokens.Count)
        {
            if (tokens[i] == "CAVOK") { period.VisibilityMeters = 10000; i++; }
            else
            {
                var mM = VisMRx.Match(tokens[i]);
                if (mM.Success)
                {
                    var v = int.Parse(mM.Groups[1].Value);
                    period.VisibilityMeters = v == 9999 ? 10000 : v;
                    i++;
                }
                else
                {
                    var mSm = VisSmRx.Match(tokens[i]);
                    if (mSm.Success) { period.VisibilityMeters = SmToMeters(mSm.Groups[1].Value); i++; }
                }
            }
        }

        // Hava durumu
        while (i < tokens.Count && WxRx.IsMatch(tokens[i]))
        {
            // WeatherCondition parse (MetarParser ile aynı mantık, tekrar etmemek için inline)
            var wx = ParseWx(tokens[i]);
            if (wx != null) period.WeatherConditions.Add(wx);
            i++;
        }

        // Bulutlar
        while (i < tokens.Count)
        {
            var m = CloudRx.Match(tokens[i]);
            if (!m.Success) break;
            if (m.Groups[4].Success) { i++; continue; } // SKC/CLR/NSC

            var altStr = m.Groups[2].Value;
            if (altStr == "///") { i++; continue; }
            if (!int.TryParse(altStr, out var alt100)) { i++; continue; }

            period.CloudLayers.Add(new CloudLayer
            {
                Coverage = Enum.Parse<CloudCoverage>(m.Groups[1].Value),
                AltitudeFt = alt100 * 100,
                Type = m.Groups[3].Success
                    ? Enum.Parse<CloudType>(m.Groups[3].Value)
                    : CloudType.None
            });
            i++;
        }
    }

    // ── Yardımcı ─────────────────────────────────────────────────────────────

    private static DateTime BuildUtcTime(int day, int hour, int minute)
    {
        var now = DateTime.UtcNow;
        var month = now.Month;
        var year = now.Year;
        if (day > now.Day) { if (month == 1) { month = 12; year--; } else month--; }
        try { return new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Utc); }
        catch { return DateTime.UtcNow; }
    }

    private static DateTime BuildPeriodTime(int day, int hour, DateTime reference, int minute = 0)
    {
        // hour == 24 → midnight → next day
        if (hour == 24)
        {
            var d = new DateTime(reference.Year, reference.Month, day, 0, minute, 0, DateTimeKind.Utc);
            return d.AddDays(1);
        }
        var month = reference.Month;
        var year = reference.Year;
        // Day wrap (e.g. validity crosses month boundary)
        if (day < reference.Day - 15)
        {
            if (month == 12) { month = 1; year++; }
            else month++;
        }
        else if (day > reference.Day + 15)
        {
            if (month == 1) { month = 12; year--; }
            else month--;
        }
        try { return new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Utc); }
        catch { return reference; }
    }

    private static int ToKnots(int v, bool isMps) => isMps ? (int)Math.Round(v * 1.94384) : v;

    private static int SmToMeters(string sm)
    {
        if (sm.Contains('/'))
        {
            var p = sm.Split('/');
            if (int.TryParse(p[0], out var n) && int.TryParse(p[1], out var d) && d != 0)
                return (int)Math.Round(n * 1609.34 / d);
            return 0;
        }
        return double.TryParse(sm, out var mi) ? (int)Math.Round(mi * 1609.34) : 0;
    }

    private static readonly string[] WxDescriptors = ["MI", "BC", "PR", "DR", "BL", "SH", "TS", "FZ"];
    private static readonly string[] WxPhenomena =
        ["RA", "SN", "DZ", "SG", "IC", "PL", "GR", "GS", "FG", "BR", "HZ", "FU", "VA", "DU", "SA", "PY", "SQ", "FC", "SS", "DS"];

    private static WeatherCondition? ParseWx(string token)
    {
        var cond = new WeatherCondition();
        var s = token;
        if (s.StartsWith('-'))       { cond.Intensity = WeatherIntensity.Light;    s = s[1..]; }
        else if (s.StartsWith('+'))  { cond.Intensity = WeatherIntensity.Heavy;    s = s[1..]; }
        else if (s.StartsWith("VC")) { cond.Intensity = WeatherIntensity.Vicinity; s = s[2..]; }
        else                           cond.Intensity = WeatherIntensity.Moderate;

        foreach (var d in WxDescriptors)
            if (s.StartsWith(d)) { cond.Descriptor = Enum.Parse<WeatherDescriptor>(d); s = s[d.Length..]; break; }

        while (s.Length >= 2)
        {
            var ok = false;
            foreach (var p in WxPhenomena)
                if (s.StartsWith(p)) { cond.Phenomena.Add(Enum.Parse<WeatherPhenomenon>(p)); s = s[p.Length..]; ok = true; break; }
            if (!ok) break;
        }
        return (cond.Phenomena.Count > 0 || cond.Descriptor != WeatherDescriptor.None) ? cond : null;
    }
}
