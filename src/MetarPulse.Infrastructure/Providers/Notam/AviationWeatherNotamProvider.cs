using System.Text.Json;
using System.Text.RegularExpressions;
using MetarPulse.Abstractions.Providers;
using MetarPulse.Core.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NotamModel = MetarPulse.Core.Models.Notam;

namespace MetarPulse.Infrastructure.Providers.Notam;

/// <summary>
/// NOAA Aviation Weather Center NOTAM API — https://aviationweather.gov/api/data/notam
/// Ücretsiz, API key gerektirmez, dünya geneli.
/// </summary>
public class AviationWeatherNotamProvider : INotamProvider
{
    public string ProviderName => "AWC_NOTAM";
    public bool IsEnabled { get; }

    private readonly string _baseUrl;

    private static readonly HttpClient _http = new(new SocketsHttpHandler
    {
        PooledConnectionLifetime = TimeSpan.FromMinutes(5)
    }) { Timeout = TimeSpan.FromSeconds(30) };

    // Q-kodu parse regex: /FIR/Q{subject}/{traffic}/{purpose}/{scope}/{lower}/{upper}/{coord}{radius}
    private static readonly Regex QLineRegex = new(
        @"/(?<fir>[A-Z]{4})/Q(?<subject>[A-Z]{4})/(?<traffic>[VIK]+)/(?<purpose>[NBOE]+)/(?<scope>[AEWK]+)/(?<lower>\d{3})/(?<upper>\d{3})/(?<coord>[\d]+[NS][\d]+[EW])(?<radius>\d+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly ILogger<AviationWeatherNotamProvider> _logger;

    public AviationWeatherNotamProvider(IConfiguration config, ILogger<AviationWeatherNotamProvider> logger)
    {
        _logger = logger;
        var section = config.GetSection("NotamProviders:AWC_NOTAM");
        IsEnabled = section.GetValue<bool>("Enabled", true);
        _baseUrl  = section.GetValue<string>("BaseUrl") ?? "https://aviationweather.gov/api/data/notam";
    }

    public async Task<IReadOnlyList<NotamModel>> GetNotamsAsync(string icao, CancellationToken ct = default)
    {
        return await FetchAsync(icao.ToUpperInvariant(), ct);
    }

    public async Task<IReadOnlyList<NotamModel>> GetNotamsAsync(IEnumerable<string> icaos, CancellationToken ct = default)
    {
        var tasks = icaos
            .Select(ic => FetchAsync(ic.ToUpperInvariant(), ct))
            .ToList();

        var results = await Task.WhenAll(tasks);
        return results.SelectMany(r => r).ToList();
    }

    public async Task<bool> HealthCheckAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await _http.GetAsync($"{_baseUrl}?ids=KJFK&format=json", ct);
            return response.IsSuccessStatusCode;
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
            var url = $"{_baseUrl}?ids={icao}&format=json";
            var response = await _http.GetAsync(url, ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("AWC NOTAM API hata: {Status} — {ICAO}", response.StatusCode, icao);
                return [];
            }

            var json = await response.Content.ReadAsStringAsync(ct);
            return ParseResponse(json, icao);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AWC NOTAM isteği başarısız: {ICAO}", icao);
            return [];
        }
    }

    private List<NotamModel> ParseResponse(string json, string icao)
    {
        var result = new List<NotamModel>();

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return result;

            foreach (var el in doc.RootElement.EnumerateArray())
            {
                var notam = ParseNotam(el, icao);
                if (notam != null)
                    result.Add(notam);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AWC NOTAM JSON parse hatası: {ICAO}", icao);
        }

        return result;
    }

    private NotamModel? ParseNotam(JsonElement el, string defaultIcao)
    {
        try
        {
            var notamId = el.TryGet("notamID") ?? el.TryGet("icaoId") ?? string.Empty;
            var rawText = el.TryGet("text") ?? el.TryGet("message") ?? el.TryGet("rawNOTAM") ?? string.Empty;

            if (string.IsNullOrWhiteSpace(rawText))
                return null;

            var icao = el.TryGet("icaoId") ?? el.TryGet("location") ?? defaultIcao;
            var qLine = el.TryGet("q") ?? string.Empty;

            // Q-kodu parse
            ParseQLine(qLine, out var fir, out var subject, out var traffic, out var scope);

            // Seri ve numara
            ParseNotamId(notamId, out var series, out var number, out var year);

            // Tarihler
            var issued = ParseDate(el.TryGet("issued"));
            var effectiveStart = ParseDate(el.TryGet("effectiveStart")) ?? issued ?? DateTime.UtcNow;
            var effectiveEndStr = el.TryGet("effectiveEnd");
            DateTime? effectiveEnd = null;
            var isPerm = false;

            if (string.IsNullOrWhiteSpace(effectiveEndStr) || effectiveEndStr == "PERM")
                isPerm = true;
            else
                effectiveEnd = ParseDate(effectiveEndStr);

            // Koordinat
            double? lat = null, lon = null;
            if (el.TryGetProperty("latitude", out var latEl) && latEl.TryGetDouble(out var latD)) lat = latD;
            if (el.TryGetProperty("longitude", out var lonEl) && lonEl.TryGetDouble(out var lonD)) lon = lonD;

            int? radius = null;
            if (el.TryGetProperty("radius", out var radEl) && radEl.TryGetInt32(out var radI)) radius = radI;

            // VFR etkisi hesapla
            var vfrImpact = NotamVfrClassifier.Classify(subject, traffic, scope, rawText);

            // Tip
            var typeStr = el.TryGet("type") ?? "N";
            var notamType = typeStr.ToUpperInvariant() switch
            {
                "R" => NotamType.Replace,
                "C" => NotamType.Cancel,
                _   => NotamType.New
            };

            return new NotamModel
            {
                NotamId        = notamId,
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
                RadiusNm       = radius,
                LowerLimit     = el.TryGet("minimumFL") ?? string.Empty,
                UpperLimit     = el.TryGet("maximumFL") ?? string.Empty,
                EffectiveFrom  = effectiveStart,
                EffectiveTo    = effectiveEnd,
                IsPermanent    = isPerm,
                Schedule       = el.TryGet("schedule"),
                RawText        = rawText.Trim(),
                VfrImpact      = vfrImpact,
                IssueDate      = issued ?? effectiveStart,
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

    // ── Q-kodu parse ─────────────────────────────────────────────────────────

    private static void ParseQLine(string qLine, out string fir, out string subject,
        out NotamTraffic traffic, out NotamScope scope)
    {
        fir = string.Empty;
        subject = string.Empty;
        traffic = NotamTraffic.All;
        scope = NotamScope.Aerodrome;

        if (string.IsNullOrWhiteSpace(qLine)) return;

        var m = QLineRegex.Match(qLine);
        if (!m.Success)
        {
            // Basit split dene: /LTAA/QMRLC/IV/NBO/A/000/060/...
            var parts = qLine.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 5)
            {
                fir = parts[0].ToUpperInvariant();
                var qPart = parts[1].StartsWith('Q') ? parts[1][1..] : parts[1];
                subject = qPart.Length >= 2 ? qPart[..2].ToUpperInvariant() : qPart.ToUpperInvariant();
                traffic = ParseTraffic(parts[2]);
                scope = ParseScope(parts[4]);
            }
            return;
        }

        fir = m.Groups["fir"].Value.ToUpperInvariant();
        var subjectFull = m.Groups["subject"].Value;
        subject = subjectFull.Length >= 2 ? subjectFull[..2].ToUpperInvariant() : subjectFull.ToUpperInvariant();
        traffic = ParseTraffic(m.Groups["traffic"].Value);
        scope = ParseScope(m.Groups["scope"].Value);
    }

    private static NotamTraffic ParseTraffic(string code) => code.ToUpperInvariant() switch
    {
        "V"  => NotamTraffic.Vfr,
        "I"  => NotamTraffic.Ifr,
        "K"  => NotamTraffic.Checklist,
        "IV" or "VI" or "IVK" or "VIK" => NotamTraffic.All,
        _    => NotamTraffic.All
    };

    private static NotamScope ParseScope(string code)
    {
        var upper = code.ToUpperInvariant();
        if (upper.Contains('E') && !upper.Contains('A')) return NotamScope.EnRoute;
        if (upper.Contains('W')) return NotamScope.Nav;
        if (upper.Contains('A')) return NotamScope.Aerodrome;
        return NotamScope.All;
    }

    private static void ParseNotamId(string notamId, out char series, out int number, out int year)
    {
        series = ' ';
        number = 0;
        year = 0;

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

    private static DateTime? ParseDate(string? dateStr)
    {
        if (string.IsNullOrWhiteSpace(dateStr)) return null;

        if (DateTime.TryParse(dateStr, null,
            System.Globalization.DateTimeStyles.RoundtripKind, out var dt))
            return dt.ToUniversalTime();

        return null;
    }
}

// ── JsonElement yardımcı uzantısı ────────────────────────────────────────────
file static class JsonElementExtensions
{
    public static string? TryGet(this JsonElement el, string property)
    {
        if (el.TryGetProperty(property, out var val) && val.ValueKind == JsonValueKind.String)
            return val.GetString();
        return null;
    }
}
