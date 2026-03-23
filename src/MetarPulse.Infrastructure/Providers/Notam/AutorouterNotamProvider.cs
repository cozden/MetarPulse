using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using MetarPulse.Abstractions.Providers;
using MetarPulse.Core.Enums;
using Microsoft.Extensions.Logging;
using NotamModel = MetarPulse.Core.Models.Notam;

namespace MetarPulse.Infrastructure.Providers.Notam;

/// <summary>
/// autorouter.aero NOTAM servisi — EUROCONTROL NM verisini yansıtır.
/// Endpoint: GET https://autorouter.aero/api/v1/notam?location={ICAO}
/// Avrupa (ECAC) meydanları için birincil EUROCONTROL kaynağı.
/// API key gerektirmez.
/// </summary>
public class AutorouterNotamProvider : INotamProvider
{
    public string ProviderName => "AUTOROUTER";
    public bool IsEnabled => true;

    private const string BaseUrl = "https://autorouter.aero/api/v1/notam";

    private static readonly HttpClient _http = new(new SocketsHttpHandler
    {
        PooledConnectionLifetime = TimeSpan.FromMinutes(5)
    })
    {
        Timeout = TimeSpan.FromSeconds(30),
        DefaultRequestHeaders =
        {
            { "User-Agent", "Mozilla/5.0 (compatible; MetarPulse/1.0)" },
            { "Accept",     "application/json" }
        }
    };

    // ICAO NOTAM metin alanları: B) YYMMDDHHMM veya tam ISO tarih
    private static readonly Regex DateYymmddRegex = new(
        @"^(\d{2})(\d{2})(\d{2})(\d{2})(\d{2})$",
        RegexOptions.Compiled);

    private readonly ILogger<AutorouterNotamProvider> _logger;

    public AutorouterNotamProvider(ILogger<AutorouterNotamProvider> logger)
    {
        _logger = logger;
    }

    public async Task<IReadOnlyList<NotamModel>> GetNotamsAsync(string icao, CancellationToken ct = default)
        => await FetchAsync(icao.ToUpperInvariant(), ct);

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
            var result = await FetchAsync("EGLL", ct);
            return true;
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
            var url = $"{BaseUrl}?location={icao}";
            var response = await _http.GetAsync(url, ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Autorouter NOTAM API hata: {Status} — {ICAO}", response.StatusCode, icao);
                return [];
            }

            var json = await response.Content.ReadAsStringAsync(ct);
            return ParseResponse(json, icao);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Autorouter NOTAM isteği başarısız: {ICAO}", icao);
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

            // Yanıt doğrudan dizi veya {"notams": [...]} formatında gelebilir
            JsonElement array;
            if (root.ValueKind == JsonValueKind.Array)
                array = root;
            else if (root.TryGetProperty("notams", out var notamsEl)
                     && notamsEl.ValueKind == JsonValueKind.Array)
                array = notamsEl;
            else
            {
                _logger.LogWarning("Autorouter: beklenmeyen JSON yapısı — {ICAO}", icao);
                return result;
            }

            foreach (var el in array.EnumerateArray())
            {
                var notam = ParseNotam(el, icao);
                if (notam != null)
                    result.Add(notam);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Autorouter NOTAM JSON parse hatası: {ICAO}", icao);
        }

        return result;
    }

    private NotamModel? ParseNotam(JsonElement el, string defaultIcao)
    {
        try
        {
            // autorouter NOTAM nesnesi ICAO NOTAM alanlarını yansıtır:
            // id/notamId = NOTAM numarası (ör. "A1234/25")
            // q          = Q satırı
            // a          = meydan ICAO
            // b          = başlangıç zamanı (YYMMDDHHMM veya ISO)
            // c          = bitiş zamanı
            // e          = NOTAM metni (E alanı)
            // type       = N/R/C

            var notamId = el.TryGet("id") ?? el.TryGet("notamId") ?? el.TryGet("series") ?? string.Empty;
            var rawText  = el.TryGet("e") ?? el.TryGet("text") ?? el.TryGet("body") ?? string.Empty;

            if (string.IsNullOrWhiteSpace(rawText) && string.IsNullOrWhiteSpace(notamId))
                return null;

            var icao  = el.TryGet("a") ?? el.TryGet("location") ?? el.TryGet("icao") ?? defaultIcao;
            var qLine = el.TryGet("q") ?? string.Empty;

            ParseQLine(qLine, out var fir, out var subject, out var traffic, out var scope);
            ParseNotamId(notamId, out var series, out var number, out var year);

            var startStr = el.TryGet("b") ?? el.TryGet("effectiveStart") ?? el.TryGet("startDate") ?? string.Empty;
            var endStr   = el.TryGet("c") ?? el.TryGet("effectiveEnd")   ?? el.TryGet("endDate")   ?? string.Empty;

            var effectiveFrom = ParseNotamDate(startStr) ?? DateTime.UtcNow;
            var isPerm        = string.IsNullOrWhiteSpace(endStr)
                                || endStr.Equals("PERM", StringComparison.OrdinalIgnoreCase)
                                || endStr.Equals("0000000000", StringComparison.Ordinal);
            DateTime? effectiveTo = isPerm ? null : ParseNotamDate(endStr);

            var typeStr  = el.TryGet("type") ?? "N";
            var notamType = typeStr.ToUpperInvariant() switch
            {
                "R" => NotamType.Replace,
                "C" => NotamType.Cancel,
                _   => NotamType.New
            };

            var vfrImpact = NotamVfrClassifier.Classify(subject, traffic, scope, rawText);

            // Koordinat — autorouter bazen koordinat döndürür
            double? lat = null, lon = null;
            if (el.TryGetProperty("lat", out var latEl) && latEl.TryGetDouble(out var latD)) lat = latD;
            if (el.TryGetProperty("lon", out var lonEl) && lonEl.TryGetDouble(out var lonD)) lon = lonD;

            int? radius = null;
            if (el.TryGetProperty("radius", out var radEl) && radEl.TryGetInt32(out var radI)) radius = radI;

            var lowerLimit = el.TryGet("f") ?? el.TryGet("lowerLimit") ?? string.Empty;
            var upperLimit = el.TryGet("g") ?? el.TryGet("upperLimit") ?? string.Empty;

            return new NotamModel
            {
                NotamId       = notamId,
                AirportIdent  = icao.ToUpperInvariant(),
                FirIdent      = fir,
                Series        = series,
                Number        = number,
                Year          = year,
                NotamType     = notamType,
                QLine         = qLine,
                Subject       = subject,
                Traffic       = traffic,
                Scope         = scope,
                Latitude      = lat,
                Longitude     = lon,
                RadiusNm      = radius,
                LowerLimit    = lowerLimit,
                UpperLimit    = upperLimit,
                EffectiveFrom = effectiveFrom,
                EffectiveTo   = effectiveTo,
                IsPermanent   = isPerm,
                Schedule      = el.TryGet("d") ?? el.TryGet("schedule"),
                RawText       = rawText.Trim(),
                VfrImpact     = vfrImpact,
                IssueDate     = effectiveFrom,
                FetchedAt     = DateTime.UtcNow,
                SourceProvider = ProviderName
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Autorouter NOTAM parse hatası, atlanıyor.");
            return null;
        }
    }

    // ── Q-kodu ───────────────────────────────────────────────────────────────

    private static void ParseQLine(string qLine, out string fir, out string subject,
        out NotamTraffic traffic, out NotamScope scope)
    {
        fir = string.Empty; subject = string.Empty;
        traffic = NotamTraffic.All; scope = NotamScope.Aerodrome;

        if (string.IsNullOrWhiteSpace(qLine)) return;

        var line  = qLine.StartsWith("Q)") ? qLine[2..].Trim() : qLine.Trim();
        var parts = line.Split('/', StringSplitOptions.None);
        if (parts.Length < 5) return;

        fir = parts[0].Trim().ToUpperInvariant();
        var qPart = parts[1].Trim();
        if (qPart.StartsWith('Q')) qPart = qPart[1..];
        subject = qPart.Length >= 2 ? qPart[..2].ToUpperInvariant() : qPart.ToUpperInvariant();
        traffic = ParseTraffic(parts[2].Trim());
        scope   = ParseScope(parts.Length > 4 ? parts[4].Trim() : "A");
    }

    private static NotamTraffic ParseTraffic(string code) => code.ToUpperInvariant() switch
    {
        "V"                             => NotamTraffic.Vfr,
        "I"                             => NotamTraffic.Ifr,
        "K"                             => NotamTraffic.Checklist,
        "IV" or "VI" or "IVK" or "VIK" => NotamTraffic.All,
        _                               => NotamTraffic.All
    };

    private static NotamScope ParseScope(string code)
    {
        var upper = code.ToUpperInvariant();
        if (upper.Contains('E') && !upper.Contains('A')) return NotamScope.EnRoute;
        if (upper.Contains('W')) return NotamScope.Nav;
        if (upper.Contains('A')) return NotamScope.Aerodrome;
        return NotamScope.All;
    }

    // ── NOTAM ID ─────────────────────────────────────────────────────────────

    private static void ParseNotamId(string notamId, out char series, out int number, out int year)
    {
        series = ' '; number = 0; year = 0;
        if (string.IsNullOrWhiteSpace(notamId)) return;

        var slash = notamId.IndexOf('/');
        if (slash <= 0) return;

        var left  = notamId[..slash];
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

    // ── Tarih parse ──────────────────────────────────────────────────────────

    /// <summary>
    /// ICAO NOTAM zamanı: YYMMDDHHMM (ör. "2502010000")
    /// veya ISO 8601 (ör. "2025-02-01T00:00:00Z")
    /// </summary>
    private DateTime? ParseNotamDate(string? dateStr)
    {
        if (string.IsNullOrWhiteSpace(dateStr)
            || dateStr.Equals("PERM", StringComparison.OrdinalIgnoreCase))
            return null;

        // YYMMDDHHMM — 10 rakam
        if (dateStr.Length == 10 && dateStr.All(char.IsDigit))
        {
            var m = DateYymmddRegex.Match(dateStr);
            if (m.Success)
            {
                var yy = int.Parse(m.Groups[1].Value);
                var month = int.Parse(m.Groups[2].Value);
                var day   = int.Parse(m.Groups[3].Value);
                var hour  = int.Parse(m.Groups[4].Value);
                var min   = int.Parse(m.Groups[5].Value);
                var fullYear = yy >= 50 ? 1900 + yy : 2000 + yy;
                return new DateTime(fullYear, month, day, hour, min, 0, DateTimeKind.Utc);
            }
        }

        // ISO / standard parse
        if (DateTime.TryParse(dateStr, null,
            System.Globalization.DateTimeStyles.RoundtripKind, out var dt))
            return dt.ToUniversalTime();

        return null;
    }
}

// ── JsonElement yardımcı uzantısı ────────────────────────────────────────────
file static class JsonElementExtAr
{
    public static string? TryGet(this JsonElement el, string property)
    {
        if (el.TryGetProperty(property, out var val) && val.ValueKind == JsonValueKind.String)
            return val.GetString();
        return null;
    }
}
