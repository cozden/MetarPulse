using Microsoft.JSInterop;

namespace MetarPulse.Maui.Services;

/// <summary>
/// Uygulama temasını yönetir.
/// Tercih SecureStorage'da saklanır; tema HTML data-theme özniteliği
/// üzerinden JS interop ile anlık olarak değiştirilir.
/// </summary>
public class ThemeService
{
    private const string StorageKey = "mp_theme";

    public const string Dark  = "night";
    public const string Light = "light";

    private readonly IJSRuntime _js;
    private string _current = Dark;

    public ThemeService(IJSRuntime js) => _js = js;

    public string Current => _current;
    public bool IsDark => _current == Dark;

    public event Action? OnThemeChanged;

    /// <summary>Uygulama açılışında SecureStorage'dan tercihi yükler ve uygular.</summary>
    public async Task InitializeAsync()
    {
        var saved = await SecureStorage.Default.GetAsync(StorageKey);
        _current = saved == Light ? Light : Dark;
        await ApplyAsync(_current);
    }

    /// <summary>Temayı değiştirir, kaydeder ve DOM'a uygular.</summary>
    public async Task SetThemeAsync(string theme)
    {
        _current = theme == Light ? Light : Dark;
        await SecureStorage.Default.SetAsync(StorageKey, _current);
        await ApplyAsync(_current);
        OnThemeChanged?.Invoke();
    }

    public Task ToggleAsync()
        => SetThemeAsync(IsDark ? Light : Dark);

    private async Task ApplyAsync(string theme)
    {
        try
        {
            await _js.InvokeVoidAsync("metarPulse.setTheme", theme);
        }
        catch { /* JS hazır değilse sessizce geç */ }
    }
}
