using Microsoft.JSInterop;

namespace MetarPulse.Maui.Services;

/// <summary>
/// METAR okunma durumunu localStorage'da saklar.
/// ICAO başına son okunan ObservationTime tutulur.
/// Yeni METAR gelince obsTime değişeceği için otomatik "okunmamış" olur.
/// </summary>
public class ReadStateService
{
    private readonly IJSRuntime _js;
    private readonly Dictionary<string, string> _cache = new();

    public event Action? StateChanged;

    public ReadStateService(IJSRuntime js) => _js = js;

    public async Task MarkReadAsync(string icao, DateTime observationTime)
    {
        var key = Key(icao);
        var value = observationTime.ToString("O");
        try
        {
            await _js.InvokeVoidAsync("localStorage.setItem", key, value);
            _cache[key] = value;
            StateChanged?.Invoke();
        }
        catch { /* WebView henüz hazır değil */ }
    }

    public async Task<bool> IsReadAsync(string icao, DateTime observationTime)
    {
        var key = Key(icao);
        if (!_cache.TryGetValue(key, out var cached))
        {
            try
            {
                cached = await _js.InvokeAsync<string?>("localStorage.getItem", key) ?? string.Empty;
                _cache[key] = cached;
            }
            catch { return false; }
        }
        return cached == observationTime.ToString("O");
    }

    // ── NOTAM okunma durumu ────────────────────────────────────────────────────

    public async Task MarkNotamUnreadAsync(string notamId)
    {
        var key = NotamKey(notamId);
        try
        {
            await _js.InvokeVoidAsync("localStorage.removeItem", key);
            _cache.Remove(key);
        }
        catch { }
    }

    public async Task MarkNotamReadAsync(string notamId)
    {
        var key = NotamKey(notamId);
        try
        {
            await _js.InvokeVoidAsync("localStorage.setItem", key, "1");
            _cache[key] = "1";
        }
        catch { }
    }

    public async Task<bool> IsNotamReadAsync(string notamId)
    {
        var key = NotamKey(notamId);
        if (!_cache.TryGetValue(key, out var cached))
        {
            try
            {
                cached = await _js.InvokeAsync<string?>("localStorage.getItem", key) ?? string.Empty;
                _cache[key] = cached;
            }
            catch { return false; }
        }
        return cached == "1";
    }

    private static string Key(string icao) => $"metar_read_{icao.ToUpperInvariant()}";
    private static string NotamKey(string notamId) => $"notam_read_{notamId}";
}
