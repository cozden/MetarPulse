using System.Text.RegularExpressions;
using MetarPulse.Core.Enums;
using MetarPulse.Core.Models;

namespace MetarPulse.Core.Services;

/// <summary>
/// Ham METAR stringini Metar modeline dönüştürür.
/// ICAO Annex 3 formatını destekler (Q-altimetre + A-altimetre).
/// </summary>
public static class MetarParser
{
    // dddssKT, dddssGggKT, VRBssKT, dddss/ssMPS
    private static readonly Regex WindRx = new(
        @"^(VRB|\d{3})(\d{2,3})(G(\d{2,3}))?(KT|MPS)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // 350V050
    private static readonly Regex VarWindRx = new(
        @"^(\d{3})V(\d{3})$", RegexOptions.Compiled);

    // 0800, 9999
    private static readonly Regex VisMRx = new(
        @"^(\d{4})$", RegexOptions.Compiled);

    // 3SM, 1/4SM, 10SM
    private static readonly Regex VisSmRx = new(
        @"^(\d+(?:/\d+)?)SM$", RegexOptions.Compiled);

    // FEW020, SCT060CB, BKN025TCU, OVC010, VV003, VV///, SKC, CLR, NSC
    private static readonly Regex CloudRx = new(
        @"^(FEW|SCT|BKN|OVC|VV)(\d{3}|///)(CB|TCU)?$|^(SKC|CLR|NSC|NCD)$",
        RegexOptions.Compiled);

    // M02/M05 or 20/10 or 10/M02
    private static readonly Regex TempRx = new(
        @"^(M?\d{1,2})/(M?\d{1,2})$", RegexOptions.Compiled);

    // Q1013 or A2992
    private static readonly Regex AltRx = new(
        @"^([QA])(\d{4})$", RegexOptions.Compiled);

    // 121150Z
    private static readonly Regex TimeRx = new(
        @"^(\d{2})(\d{2})(\d{2})Z$", RegexOptions.Compiled);

    // R28R/0600FT, R10/P2000, R28L/M0600FT/D
    private static readonly Regex RvrRx = new(
        @"^R\d{2}[LRC]?/[MP]?\d{4}(FT)?(/[UDN])?$", RegexOptions.Compiled);

    // Weather token: phenomenon isteğe bağlı — VCTS gibi descriptor-only gruplar desteklenir
    private static readonly Regex WxRx = new(
        @"^[-+]?(VC)?((MI|BC|PR|DR|BL|SH|TS|FZ)|(RA|SN|DZ|SG|IC|PL|GR|GS|FG|BR|HZ|FU|VA|DU|SA|PY|SQ|FC|SS|DS)){1,}$",
        RegexOptions.Compiled);

    private static readonly string[] WxDescriptors = ["MI", "BC", "PR", "DR", "BL", "SH", "TS", "FZ"];
    private static readonly string[] WxPhenomena =
        ["RA", "SN", "DZ", "SG", "IC", "PL", "GR", "GS", "FG", "BR", "HZ", "FU", "VA", "DU", "SA", "PY", "SQ", "FC", "SS", "DS"];

    public static Metar Parse(string rawText, string? sourceProvider = null)
    {
        var metar = new Metar
        {
            RawText = rawText.Trim(),
            SourceProvider = sourceProvider,
            FetchedAt = DateTime.UtcNow
        };

        // RMK bölümünü kaldır
        var text = metar.RawText;
        var rmkIdx = text.IndexOf(" RMK ", StringComparison.OrdinalIgnoreCase);
        if (rmkIdx > 0) text = text[..rmkIdx];

        var tokens = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        int i = 0;

        // 1. Tip: METAR / SPECI
        if (i < tokens.Length && tokens[i] is "METAR" or "SPECI")
        {
            metar.IsSpeci = tokens[i] == "SPECI";
            i++;
            if (i < tokens.Length && tokens[i] is "COR" or "AMD") i++;
        }

        // 2. İstasyon (4-char ICAO)
        if (i < tokens.Length && tokens[i].Length == 4 && tokens[i].All(char.IsLetterOrDigit))
        {
            metar.StationId = tokens[i].ToUpper();
            i++;
        }

        // 3. Zaman: DDHHMMz
        if (i < tokens.Length)
        {
            var m = TimeRx.Match(tokens[i]);
            if (m.Success)
            {
                metar.ObservationTime = BuildObservationTime(
                    int.Parse(m.Groups[1].Value),
                    int.Parse(m.Groups[2].Value),
                    int.Parse(m.Groups[3].Value));
                i++;
            }
        }

        // 4. AUTO / NIL / COR
        while (i < tokens.Length && tokens[i] is "AUTO" or "NIL" or "COR" or "AMD" or "CORR") i++;

        // 5. Rüzgar
        if (i < tokens.Length)
        {
            var m = WindRx.Match(tokens[i]);
            if (m.Success)
            {
                bool isMps = m.Groups[5].Value.Equals("MPS", StringComparison.OrdinalIgnoreCase);
                metar.IsVariableWind = m.Groups[1].Value == "VRB";
                metar.WindDirection = metar.IsVariableWind ? 0 : int.Parse(m.Groups[1].Value);
                metar.WindSpeed = ToKnots(int.Parse(m.Groups[2].Value), isMps);
                if (m.Groups[3].Success)
                    metar.WindGust = ToKnots(int.Parse(m.Groups[4].Value), isMps);
                i++;
            }
        }

        // 6. Değişken rüzgar sektörü (350V050)
        if (i < tokens.Length)
        {
            var m = VarWindRx.Match(tokens[i]);
            if (m.Success)
            {
                metar.IsVariableWind = true;
                metar.VariableWindFrom = int.Parse(m.Groups[1].Value);
                metar.VariableWindTo = int.Parse(m.Groups[2].Value);
                i++;
            }
        }

        // 7. Görüş
        if (i < tokens.Length)
        {
            if (tokens[i] == "CAVOK")
            {
                metar.VisibilityMeters = 10000;
                metar.IsCavok = true;
                i++;
            }
            else if (tokens[i] is "////" or "/////" )
            {
                metar.VisibilityMeters = 0;
                i++;
            }
            else
            {
                var mM = VisMRx.Match(tokens[i]);
                if (mM.Success)
                {
                    var v = int.Parse(mM.Groups[1].Value);
                    metar.VisibilityMeters = v == 9999 ? 10000 : v;
                    i++;
                    if (i < tokens.Length && tokens[i] == "NDV") i++;
                }
                else
                {
                    var mSm = VisSmRx.Match(tokens[i]);
                    if (mSm.Success)
                    {
                        metar.VisibilityMeters = SmToMeters(mSm.Groups[1].Value);
                        i++;
                    }
                }
            }
        }

        // 8. RVR (birden fazla olabilir)
        while (i < tokens.Length && RvrRx.IsMatch(tokens[i]))
        {
            metar.RvrRaw = metar.RvrRaw == null
                ? tokens[i]
                : metar.RvrRaw + " " + tokens[i];
            i++;
        }

        // 9. Hava durumu fenomenleri
        while (i < tokens.Length && WxRx.IsMatch(tokens[i]))
        {
            var wx = ParseWeatherCondition(tokens[i]);
            if (wx != null) metar.WeatherConditions.Add(wx);
            i++;
        }

        // 10. Bulut katmanları
        while (i < tokens.Length)
        {
            var m = CloudRx.Match(tokens[i]);
            if (!m.Success) break;

            // SKC / CLR / NSC / NCD
            if (m.Groups[4].Success) { i++; continue; }

            var coverageStr = m.Groups[1].Value;
            var altStr = m.Groups[2].Value;

            // VV/// veya altitude bilinmiyorsa
            if (altStr == "///") { i++; continue; }

            if (!int.TryParse(altStr, out var alt100)) { i++; continue; }

            var layer = new CloudLayer
            {
                Coverage = Enum.Parse<CloudCoverage>(coverageStr),
                AltitudeFt = alt100 * 100,
                Type = m.Groups[3].Success
                    ? Enum.Parse<CloudType>(m.Groups[3].Value)
                    : CloudType.None
            };
            metar.CloudLayers.Add(layer);
            i++;
        }

        // 11. Sıcaklık / Çiy noktası
        if (i < tokens.Length)
        {
            var m = TempRx.Match(tokens[i]);
            if (m.Success)
            {
                metar.Temperature = ParseTemp(m.Groups[1].Value);
                metar.DewPoint = ParseTemp(m.Groups[2].Value);
                i++;
            }
        }

        // 12. Basınç (QNH)
        if (i < tokens.Length)
        {
            var m = AltRx.Match(tokens[i]);
            if (m.Success)
            {
                var val = int.Parse(m.Groups[2].Value);
                if (m.Groups[1].Value == "Q")
                    metar.AltimeterHpa = val;
                else
                    metar.AltimeterInHg = val / 100m;
                i++;
            }
        }

        // 13. Trend
        while (i < tokens.Length)
        {
            if (tokens[i] is "NOSIG" or "BECMG" or "TEMPO")
            {
                metar.Trend = tokens[i];
                break;
            }
            i++;
        }

        // 14. Tavan ve uçuş kategorisi hesapla
        metar.CeilingFeet = FlightCategoryResolver.CalculateCeiling(metar.CloudLayers);
        metar.Category = FlightCategoryResolver.Resolve(metar.VisibilityMeters, metar.CeilingFeet);

        return metar;
    }

    // ── Yardımcı metodlar ────────────────────────────────────────────────────

    private static DateTime BuildObservationTime(int day, int hour, int minute)
    {
        var now = DateTime.UtcNow;
        var month = now.Month;
        var year = now.Year;

        if (day > now.Day)
        {
            // Geçen ay
            if (month == 1) { month = 12; year--; }
            else month--;
        }

        try
        {
            return new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Utc);
        }
        catch
        {
            return DateTime.UtcNow;
        }
    }

    private static int ToKnots(int value, bool isMps)
        => isMps ? (int)Math.Round(value * 1.94384) : value;

    private static int SmToMeters(string sm)
    {
        if (sm.Contains('/'))
        {
            var parts = sm.Split('/');
            if (int.TryParse(parts[0], out var num) && int.TryParse(parts[1], out var den) && den != 0)
                return (int)Math.Round(num * 1609.34 / den);
            return 0;
        }
        if (double.TryParse(sm, out var miles))
            return (int)Math.Round(miles * 1609.34);
        return 0;
    }

    private static WeatherCondition? ParseWeatherCondition(string token)
    {
        var cond = new WeatherCondition();
        var s = token;

        // Yoğunluk
        if (s.StartsWith('-'))      { cond.Intensity = WeatherIntensity.Light;    s = s[1..]; }
        else if (s.StartsWith('+')) { cond.Intensity = WeatherIntensity.Heavy;    s = s[1..]; }
        else if (s.StartsWith("VC")){ cond.Intensity = WeatherIntensity.Vicinity; s = s[2..]; }
        else                          cond.Intensity = WeatherIntensity.Moderate;

        // Tanımlayıcı (descriptor)
        foreach (var d in WxDescriptors)
        {
            if (s.StartsWith(d))
            {
                cond.Descriptor = Enum.Parse<WeatherDescriptor>(d);
                s = s[d.Length..];
                break;
            }
        }

        // Fenomenler (2-char, birden fazla olabilir: RASN)
        while (s.Length >= 2)
        {
            var matched = false;
            foreach (var p in WxPhenomena)
            {
                if (s.StartsWith(p))
                {
                    cond.Phenomena.Add(Enum.Parse<WeatherPhenomenon>(p));
                    s = s[p.Length..];
                    matched = true;
                    break;
                }
            }
            if (!matched) break;
        }

        // VCTS gibi descriptor-only gruplar (phenomenon yok) da geçerli
        return (cond.Phenomena.Count > 0 || cond.Descriptor != WeatherDescriptor.None) ? cond : null;
    }

    private static int ParseTemp(string s)
        => s.StartsWith('M') ? -int.Parse(s[1..]) : int.Parse(s);
}
