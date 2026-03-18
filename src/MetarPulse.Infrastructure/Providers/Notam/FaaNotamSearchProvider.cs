using System.Globalization;
using System.Text.Json;
using MetarPulse.Abstractions.Providers;
using MetarPulse.Core.Enums;
using Microsoft.Extensions.Logging;
using NotamModel = MetarPulse.Core.Models.Notam;

namespace MetarPulse.Infrastructure.Providers.Notam;

/// <summary>
/// FAA NOTAM Search (notams.aim.faa.gov) — ücretsiz, API key gerektirmez.
/// Dünya geneli NOTAM verisi (ICAO exchange dahil Türkiye/Avrupa).
/// POST /notamSearch/search endpoint — browser User-Agent gerekli.
/// </summary>
public class FaaNotamSearchProvider : INotamProvider
{
    public string ProviderName => "FAA_NOTAM";
    public bool IsEnabled => true;

    private const string BaseUrl = "https://notams.aim.faa.gov/notamSearch/search";
    private const int PageSize = 300; // Tek istekte max NOTAM sayısı

    private static readonly HttpClient _http = new(new SocketsHttpHandler
    {
        PooledConnectionLifetime = TimeSpan.FromMinutes(5)
    })
    {
        Timeout = TimeSpan.FromSeconds(30),
        DefaultRequestHeaders =
        {
            { "User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/120" },
            { "Accept", "application/json, text/javascript, */*; q=0.01" },
            { "X-Requested-With", "XMLHttpRequest" },
            { "Origin", "https://notams.aim.faa.gov" },
            { "Referer", "https://notams.aim.faa.gov/notamSearch/" }
        }
    };

    private readonly ILogger<FaaNotamSearchProvider> _logger;

    public FaaNotamSearchProvider(ILogger<FaaNotamSearchProvider> logger)
    {
        _logger = logger;
    }

    public async Task<IReadOnlyList<NotamModel>> GetNotamsAsync(string icao, CancellationToken ct = default)
    {
        return await FetchAsync(icao.ToUpperInvariant(), ct);
    }

    public async Task<IReadOnlyList<NotamModel>> GetNotamsAsync(IEnumerable<string> icaos, CancellationToken ct = default)
    {
        var tasks = icaos.Select(ic => FetchAsync(ic.ToUpperInvariant(), ct)).ToList();
        var results = await Task.WhenAll(tasks);
        return results.SelectMany(r => r).ToList();
    }

    public async Task<bool> HealthCheckAsync(CancellationToken ct = default)
    {
        try
        {
            var result = await FetchAsync("KJFK", ct);
            return true; // bağlantı kuruldu (boş sonuç da tamam)
        }
        catch
        {
            return false;
        }
    }

    // ── İç metodlar ──────────────────────────────────────────────────────────

    private async Task<IReadOnlyList<NotamModel>> FetchAsync(string icao, CancellationToken ct)
    {
        try
        {
            var body = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["searchType"]              = "0",
                ["designatorsForLocation"]  = icao,
                ["radiusInNM"]              = "0",
                ["notamOffset"]             = "0",
                ["notamsPerPage"]           = PageSize.ToString(),
                ["notamType"]               = "ALL",
                ["latLongEntered"]          = "false",
                ["latDegrees"]              = "",
                ["latMinutes"]              = "",
                ["latSeconds"]              = "",
                ["longDegrees"]             = "",
                ["longMinutes"]             = "",
                ["longSeconds"]             = ""
            });

            var response = await _http.PostAsync(BaseUrl, body, ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("FAA NOTAM API hata: {Status} — {ICAO}", response.StatusCode, icao);
                return [];
            }

            var json = await response.Content.ReadAsStringAsync(ct);
            return ParseResponse(json, icao);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "FAA NOTAM isteği başarısız: {ICAO}", icao);
            return [];
        }
    }

    private List<NotamModel> ParseResponse(string json, string icao)
    {
        var result = new List<NotamModel>();

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("notamList", out var notamList)
                || notamList.ValueKind != JsonValueKind.Array)
                return result;

            foreach (var el in notamList.EnumerateArray())
            {
                var notam = ParseNotam(el, icao);
                if (notam != null)
                    result.Add(notam);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "FAA NOTAM JSON parse hatası: {ICAO}", icao);
        }

        return result;
    }

    private NotamModel? ParseNotam(JsonElement el, string defaultIcao)
    {
        try
        {
            // Aktif olmayanları atla
            var status = el.TryGet("status") ?? string.Empty;
            var cancelled = el.TryGetBool("cancelledOrExpired");
            if (cancelled == true) return null;

            var notamNumber = el.TryGet("notamNumber") ?? string.Empty;
            var icaoMsg = el.TryGet("icaoMessage") ?? el.TryGet("traditionalMessageFrom4thWord") ?? string.Empty;
            if (string.IsNullOrWhiteSpace(icaoMsg) && string.IsNullOrWhiteSpace(notamNumber))
                return null;

            var icao = el.TryGet("icaoId") ?? el.TryGet("facilityDesignator") ?? defaultIcao;

            // Q-satırını icaoMessage'dan çıkar
            var qLine = ExtractQLine(icaoMsg);
            ParseQLine(qLine, out var fir, out var subject, out var traffic, out var scope);

            // Tarihler — FAA formatı: "MM/DD/YYYY HHmm"
            var startStr  = el.TryGet("startDate") ?? el.TryGet("issueDate") ?? string.Empty;
            var endStr    = el.TryGet("endDate") ?? string.Empty;

            var effectiveFrom = ParseFaaDate(startStr) ?? DateTime.UtcNow;
            var isPerm = endStr.Equals("PERM", StringComparison.OrdinalIgnoreCase);
            DateTime? effectiveTo = isPerm ? null : ParseFaaDate(endStr);

            // Koordinat
            double? lat = null, lon = null;
            if (el.TryGetProperty("mapPointer", out var ptr) && ptr.ValueKind == JsonValueKind.String)
            {
                var mapPoint = ptr.GetString() ?? string.Empty;
                // Format: "POINT(lon lat)"
                ParseMapPoint(mapPoint, out lon, out lat);
            }

            // VFR etkisi
            var vfrImpact = NotamVfrClassifier.Classify(subject, traffic, scope);

            // Seri & numara
            ParseNotamId(notamNumber, out var series, out var number, out var year);

            // Tip
            var notamType = notamNumber.Contains("NOTAMR") || (icaoMsg.Contains("NOTAMR"))
                ? NotamType.Replace
                : notamNumber.Contains("NOTAMC") || icaoMsg.Contains("NOTAMC")
                    ? NotamType.Cancel
                    : NotamType.New;

            return new NotamModel
            {
                NotamId        = notamNumber,
                AirportIdent   = icao.ToUpperInvariant(),
                FirIdent       = fir,
                Series         = series,
                Number         = number,
                Year           = year,
                NotamType      = notamType,
                QLine          = qLine,
                Subject        = subject,
                Traffic        = traffic,
                Scope          = scope,
                Latitude       = lat,
                Longitude      = lon,
                LowerLimit     = string.Empty,
                UpperLimit     = string.Empty,
                EffectiveFrom  = effectiveFrom,
                EffectiveTo    = effectiveTo,
                IsPermanent    = isPerm,
                RawText        = icaoMsg.Trim(),
                VfrImpact      = vfrImpact,
                IssueDate      = ParseFaaDate(el.TryGet("issueDate") ?? string.Empty) ?? effectiveFrom,
                FetchedAt      = DateTime.UtcNow,
                SourceProvider = ProviderName
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "NOTAM parse hatası, atlanıyor.");
            return null;
        }
    }

    // ── Yardımcı metodlar ─────────────────────────────────────────────────────

    private static string ExtractQLine(string icaoMsg)
    {
        // icaoMessage içinde "Q) LTBB/QXXX..." satırını bul
        var qIdx = icaoMsg.IndexOf("Q)", StringComparison.Ordinal);
        if (qIdx < 0) return string.Empty;

        var lineEnd = icaoMsg.IndexOf('\n', qIdx);
        return lineEnd < 0
            ? icaoMsg[qIdx..]
            : icaoMsg[qIdx..lineEnd].Trim();
    }

    private static void ParseQLine(string qLine, out string fir, out string subject,
        out NotamTraffic traffic, out NotamScope scope)
    {
        fir = string.Empty;
        subject = string.Empty;
        traffic = NotamTraffic.All;
        scope = NotamScope.Aerodrome;

        if (string.IsNullOrWhiteSpace(qLine)) return;

        // "Q) LTBB/QFUAR/IV/NBO/A /000/999/..." → remove "Q) "
        var line = qLine.StartsWith("Q)") ? qLine[2..].Trim() : qLine.Trim();
        var parts = line.Split('/', StringSplitOptions.None);

        if (parts.Length < 5) return;

        fir = parts[0].Trim().ToUpperInvariant();
        var qPart = parts[1].Trim();
        if (qPart.StartsWith('Q')) qPart = qPart[1..];
        subject = qPart.Length >= 2 ? qPart[..2].ToUpperInvariant() : qPart.ToUpperInvariant();

        traffic = ParseTraffic(parts[2].Trim());
        scope = ParseScope(parts.Length > 4 ? parts[4].Trim() : "A");
    }

    private static NotamTraffic ParseTraffic(string code) => code.Trim().ToUpperInvariant() switch
    {
        "V"  => NotamTraffic.Vfr,
        "I"  => NotamTraffic.Ifr,
        "K"  => NotamTraffic.Checklist,
        "IV" or "VI" or "IVK" or "VIK" => NotamTraffic.All,
        _ => NotamTraffic.All
    };

    private static NotamScope ParseScope(string code)
    {
        var upper = code.Trim().ToUpperInvariant();
        if (upper.Contains('E') && !upper.Contains('A')) return NotamScope.EnRoute;
        if (upper.Contains('W')) return NotamScope.Nav;
        if (upper.Contains('A')) return NotamScope.Aerodrome;
        return NotamScope.All;
    }

    private static DateTime? ParseFaaDate(string? dateStr)
    {
        if (string.IsNullOrWhiteSpace(dateStr) || dateStr.Equals("PERM", StringComparison.OrdinalIgnoreCase))
            return null;

        // Format: "12/31/2025 0740" veya "12/31/2025 0740EST"
        var clean = dateStr.Trim();
        // EST gibi timezone suffix'i kaldır
        var tzIdx = clean.IndexOfAny([' '], 14);
        if (tzIdx > 0 && !char.IsDigit(clean[tzIdx + 1]))
            clean = clean[..tzIdx].Trim();

        if (DateTime.TryParseExact(clean, "MM/dd/yyyy HHmm",
            CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var dt))
            return dt.ToUniversalTime();

        // Fallback: standard parse
        if (DateTime.TryParse(clean, null, DateTimeStyles.AssumeUniversal, out var dt2))
            return dt2.ToUniversalTime();

        return null;
    }

    private static void ParseMapPoint(string mapPoint, out double? lon, out double? lat)
    {
        lon = null; lat = null;
        // "POINT(27.919094 41.13825)"
        var inner = mapPoint.Replace("POINT(", "").TrimEnd(')');
        var parts = inner.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2
            && double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var lo)
            && double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var la))
        {
            lon = lo; lat = la;
        }
    }

    private static void ParseNotamId(string notamId, out char series, out int number, out int year)
    {
        series = ' '; number = 0; year = 0;
        if (string.IsNullOrWhiteSpace(notamId)) return;

        var slash = notamId.IndexOf('/');
        if (slash <= 0) return;

        var left = notamId[..slash];
        var right = notamId[(slash + 1)..];

        if (left.Length > 0 && char.IsLetter(left[0]))
        {
            series = char.ToUpperInvariant(left[0]);
            int.TryParse(left[1..], out number);
        }
        else
        {
            int.TryParse(left, out number);
        }

        int.TryParse(right, out year);
    }
}

// ── JsonElement yardımcı uzantısı ────────────────────────────────────────────
file static class JsonElementExt
{
    public static string? TryGet(this JsonElement el, string property)
    {
        if (el.TryGetProperty(property, out var val) && val.ValueKind == JsonValueKind.String)
            return val.GetString();
        return null;
    }

    public static bool? TryGetBool(this JsonElement el, string property)
    {
        if (el.TryGetProperty(property, out var val) && val.ValueKind == JsonValueKind.True) return true;
        if (el.TryGetProperty(property, out val) && val.ValueKind == JsonValueKind.False) return false;
        return null;
    }
}
