using System.Net.Http.Json;
using MetarPulse.Maui.Models;

namespace MetarPulse.Maui.Services;

/// <summary>
/// MetarPulse REST API istemcisi.
/// BaseUrl, appsettings.json "Api:BaseUrl" anahtarından alınır;
/// sağlanmamışsa Android emülatör adresi varsayılan olarak kullanılır.
/// </summary>
public class ApiService
{
    private readonly HttpClient _http;

    public ApiService(HttpClient http)
    {
        _http = http;
    }

    // ── METAR ────────────────────────────────────────────────────────────────

    public async Task<MetarViewModel?> GetMetarAsync(string icao, CancellationToken ct = default)
    {
        try
        {
            var dto = await _http.GetFromJsonAsync<MetarDto>($"api/metar/{icao.ToUpperInvariant()}", ct);
            return dto is null ? null : MapMetar(dto);
        }
        catch { return null; }
    }

    public async Task<List<MetarViewModel>> GetBulkMetarAsync(IEnumerable<string> icaoCodes, CancellationToken ct = default)
    {
        try
        {
            var joined = string.Join(",", icaoCodes.Select(c => c.ToUpperInvariant()));
            var dtos = await _http.GetFromJsonAsync<List<MetarDto>>($"api/metar/bulk?icao={joined}", ct);
            return dtos?.Select(MapMetar).ToList() ?? [];
        }
        catch { return []; }
    }

    public async Task<MetarViewModel?> RefreshMetarAsync(string icao, CancellationToken ct = default)
    {
        try
        {
            var response = await _http.PostAsync($"api/metar/{icao.ToUpperInvariant()}/refresh", null, ct);
            if (!response.IsSuccessStatusCode) return null;

            var result = await response.Content.ReadFromJsonAsync<RefreshResult>(cancellationToken: ct);
            return result?.Metar is null ? null : MapMetar(result.Metar);
        }
        catch { return null; }
    }

    // ── Bookmark ──────────────────────────────────────────────────────────────

    public async Task<List<string>> GetBookmarkIcaosAsync(CancellationToken ct = default)
    {
        try
        {
            return await _http.GetFromJsonAsync<List<string>>("api/bookmarks/icaos", ct) ?? [];
        }
        catch { return []; }
    }

    public async Task<bool> AddBookmarkAsync(string icao, CancellationToken ct = default)
    {
        try
        {
            var response = await _http.PostAsJsonAsync("api/bookmarks",
                new { IcaoCode = icao.ToUpperInvariant(), DisplayOrder = 999 }, ct);
            return response.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    public async Task<bool> RemoveBookmarkAsync(string icao, CancellationToken ct = default)
    {
        try
        {
            var response = await _http.DeleteAsync($"api/bookmarks/{icao.ToUpperInvariant()}", ct);
            return response.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    // ── Mapping ───────────────────────────────────────────────────────────────

    private static MetarViewModel MapMetar(MetarDto d) => new()
    {
        StationId       = d.StationId,
        RawText         = d.RawText,
        ObservationTime = d.ObservationTime,
        WindDirection   = d.WindDirection,
        WindSpeed       = d.WindSpeed,
        WindGust        = d.WindGust,
        IsVariableWind  = d.IsVariableWind,
        VariableWindFrom = d.VariableWindFrom,
        VariableWindTo  = d.VariableWindTo,
        VisibilityMeters = d.VisibilityMeters,
        CeilingFeet     = d.CeilingFeet,
        Category        = d.Category,
        Temperature     = d.Temperature,
        DewPoint        = d.DewPoint,
        AltimeterHpa    = d.AltimeterHpa,
        AltimeterInHg   = d.AltimeterInHg,
        Trend           = d.Trend,
        IsSpeci         = d.IsSpeci,
        SourceProvider  = d.SourceProvider,
        FetchedAt       = d.FetchedAt,
        IsStale         = d.IsStale,
        CloudLayers     = d.CloudLayers.Select(c => new CloudLayerViewModel
        {
            Coverage   = c.Coverage,
            AltitudeFt = c.AltitudeFt,
            Type       = c.Type
        }).ToList(),
        WeatherConditions = d.WeatherConditions
    };

    // ── TAF ──────────────────────────────────────────────────────────────────

    public async Task<TafResult?> GetTafAsync(string icao, CancellationToken ct = default)
    {
        try
        {
            return await _http.GetFromJsonAsync<TafResult>(
                $"api/metar/{icao.ToUpperInvariant()}/taf", ct);
        }
        catch { return null; }
    }

    // ── Runway ───────────────────────────────────────────────────────────────

    public async Task<RunwayWindResult?> GetRunwayWindAsync(string icao, CancellationToken ct = default)
    {
        try
        {
            return await _http.GetFromJsonAsync<RunwayWindResult>(
                $"api/runway/{icao.ToUpperInvariant()}/wind", ct);
        }
        catch { return null; }
    }

    // ── Providers ─────────────────────────────────────────────────────────────

    public async Task<List<ProviderInfo>> GetProvidersAsync(CancellationToken ct = default)
    {
        try { return await _http.GetFromJsonAsync<List<ProviderInfo>>("api/providers", ct) ?? []; }
        catch { return []; }
    }

    public async Task<bool> SetProviderEnabledAsync(string name, bool enabled, CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.PostAsync($"api/providers/{Uri.EscapeDataString(name)}/enable?enabled={enabled}", null, ct);
            return resp.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    public async Task<bool> ReorderProvidersAsync(List<string> globalOrder, List<string> turkeyOrder, CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync("api/providers/reorder",
                new { GlobalOrder = globalOrder, TurkeyOrder = turkeyOrder }, ct);
            return resp.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    // ── Notifications ─────────────────────────────────────────────────────────

    public async Task<List<NotificationPref>> GetAllNotificationPrefsAsync(CancellationToken ct = default)
    {
        try { return await _http.GetFromJsonAsync<List<NotificationPref>>("api/notifications", ct) ?? []; }
        catch { return []; }
    }

    public async Task<NotificationPref?> GetNotificationPrefAsync(string icao, CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.GetAsync($"api/notifications/{icao.ToUpperInvariant()}", ct);
            if (resp.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
            return await resp.Content.ReadFromJsonAsync<NotificationPref>(cancellationToken: ct);
        }
        catch { return null; }
    }

    public async Task<(bool Ok, string? Error)> UpsertNotificationPrefAsync(NotificationPref pref, CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync("api/notifications", pref, ct);
            if (resp.IsSuccessStatusCode) return (true, null);
            var body = await resp.Content.ReadAsStringAsync(ct);
            return (false, $"HTTP {(int)resp.StatusCode}: {body}");
        }
        catch (Exception ex) { return (false, ex.Message); }
    }

    public async Task<bool> DeleteNotificationPrefAsync(string icao, CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.DeleteAsync($"api/notifications/{icao.ToUpperInvariant()}", ct);
            return resp.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    // ── NOTAM ─────────────────────────────────────────────────────────────────

    public async Task<List<NotamViewModel>> GetNotamsAsync(string icao, CancellationToken ct = default)
    {
        try
        {
            var dtos = await _http.GetFromJsonAsync<List<NotamDto>>(
                $"api/notam/{icao.ToUpperInvariant()}", ct);
            return dtos?.Select(MapNotam).ToList() ?? [];
        }
        catch { return []; }
    }

    public async Task<List<NotamSummaryViewModel>> GetBulkNotamSummaryAsync(
        IEnumerable<string> icaoCodes, CancellationToken ct = default)
    {
        try
        {
            var joined = string.Join(",", icaoCodes.Select(c => c.ToUpperInvariant()));
            var dtos = await _http.GetFromJsonAsync<List<NotamSummaryDto>>(
                $"api/notam/bulk?icaos={joined}", ct);
            return dtos?.Select(d => new NotamSummaryViewModel
            {
                AirportIdent  = d.AirportIdent,
                ActiveCount   = d.ActiveCount,
                HasVfrWarning = d.HasVfrWarning,
                HasVfrCaution = d.HasVfrCaution
            }).ToList() ?? [];
        }
        catch { return []; }
    }

    private static NotamViewModel MapNotam(NotamDto d) => new()
    {
        NotamId        = d.NotamId,
        AirportIdent   = d.AirportIdent,
        Subject        = d.Subject,
        Traffic        = d.Traffic,
        Scope          = d.Scope,
        VfrImpact      = d.VfrImpact,
        EffectiveFrom  = d.EffectiveFrom,
        EffectiveTo    = d.EffectiveTo,
        IsPermanent    = d.IsPermanent,
        Schedule       = d.Schedule,
        LowerLimit     = d.LowerLimit,
        UpperLimit     = d.UpperLimit,
        RawText        = d.RawText,
        SourceProvider = d.SourceProvider
    };

    // ── Airport search ────────────────────────────────────────────────────────

    public async Task<List<AirportSearchResult>> SearchAirportsAsync(string query, CancellationToken ct = default)
    {
        try
        {
            if (query.Length < 2) return [];
            var results = await _http.GetFromJsonAsync<List<AirportSearchResult>>(
                $"api/airport/search?q={Uri.EscapeDataString(query)}", ct);
            return results ?? [];
        }
        catch { return []; }
    }

    /// <summary>FCM token'ını API'ye kaydeder (upsert).</summary>
    public async Task RegisterDeviceTokenAsync(string token, CancellationToken ct = default)
    {
        try
        {
            await _http.PostAsJsonAsync("api/devices/token",
                new { Token = token, Platform = "android" }, ct);
        }
        catch { /* sessizce geç */ }
    }

    /// <summary>FCM token'ını API'den siler (logout).</summary>
    public async Task UnregisterDeviceTokenAsync(string token, CancellationToken ct = default)
    {
        try
        {
            var req = new HttpRequestMessage(HttpMethod.Delete, "api/devices/token")
            {
                Content = JsonContent.Create(new { Token = token })
            };
            await _http.SendAsync(req, ct);
        }
        catch { /* sessizce geç */ }
    }

    // ── Private DTO records (API contract) ───────────────────────────────────

    private record NotamDto(
        string NotamId,
        string AirportIdent,
        string Subject,
        string Traffic,
        string Scope,
        string VfrImpact,
        DateTime EffectiveFrom,
        DateTime? EffectiveTo,
        bool IsPermanent,
        string? Schedule,
        string LowerLimit,
        string UpperLimit,
        string RawText,
        string SourceProvider
    );

    private record NotamSummaryDto(
        string AirportIdent,
        int ActiveCount,
        bool HasVfrWarning,
        bool HasVfrCaution
    );

    private record MetarDto(
        string StationId,
        string RawText,
        DateTime ObservationTime,
        int WindDirection,
        int WindSpeed,
        int? WindGust,
        bool IsVariableWind,
        int? VariableWindFrom,
        int? VariableWindTo,
        int VisibilityMeters,
        int CeilingFeet,
        string Category,
        int? Temperature,
        int? DewPoint,
        decimal? AltimeterHpa,
        decimal? AltimeterInHg,
        string? Trend,
        bool IsSpeci,
        string? SourceProvider,
        DateTime FetchedAt,
        List<CloudLayerDto> CloudLayers,
        List<string> WeatherConditions,
        bool IsStale = false
    );

    private record CloudLayerDto(string Coverage, int AltitudeFt, string Type);

    private record RefreshResult(MetarDto Metar, bool Changed);
}

// ── MAUI-side DTO'lar (API contract mirror) ───────────────────────────────────

public record RunwayWindResult(
    string StationId,
    int WindDirection,
    int WindSpeed,
    int? WindGust,
    List<RunwayWindItem> Runways);

public record RunwayWindItem(
    string RunwayIdent,
    int HeadingDeg,
    double HeadwindKnots,
    double CrosswindKnots,
    double TailwindKnots,
    bool IsTailwind,
    int? LengthFt);

public record TafResult(
    string StationId,
    string RawText,
    DateTime IssueTime,
    DateTime ValidFrom,
    DateTime ValidTo,
    string? SourceProvider,
    List<TafPeriodDto> Periods);

public record TafPeriodDto(
    DateTime From,
    DateTime To,
    string ChangeIndicator,
    int? Probability,
    int? WindDirection,
    int? WindSpeed,
    int? WindGust,
    int? VisibilityMeters,
    List<TafCloudLayerDto> CloudLayers,
    List<string> WeatherConditions);

public record TafCloudLayerDto(string Coverage, int AltitudeFt, string Type);

public record AirportSearchResult(
    string Ident,
    string Name,
    string Type,
    string IsoCountry,
    string? Municipality,
    string? IataCode,
    double LatitudeDeg,
    double LongitudeDeg);

public class ProviderInfo
{
    public string ProviderName { get; set; } = string.Empty;
    public int Priority { get; set; }
    public bool IsEnabled { get; set; }
    public bool IsHealthy { get; set; }
}

public class NotificationPref
{
    public string StationIcao { get; set; } = string.Empty;
    public bool NotifyOnCategoryChange { get; set; } = true;
    public bool NotifyOnSpeci { get; set; } = true;
    public bool NotifyOnVfrAchieved { get; set; } = true;
    public bool NotifyOnSignificantWeather { get; set; } = true;
    public bool NotifyOnEveryMetar { get; set; } = false;
    public int? WindThresholdKnots { get; set; }
    public int? VisibilityThresholdMeters { get; set; }
    public int? CeilingThresholdFeet { get; set; }

    // Zaman filtresi — 0=Pazar, 1=Pzt … 6=Cmt (DayOfWeek enum değerleri)
    public List<int> ActiveDays { get; set; } = [0, 1, 2, 3, 4, 5, 6];
    // "HH:mm:ss" formatında (API TimeOnly döner; saniyeyi görmezden gel)
    public string StartTime { get; set; } = "06:00:00";
    public string EndTime { get; set; } = "22:00:00";
}
