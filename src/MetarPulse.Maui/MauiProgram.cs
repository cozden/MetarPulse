using CommunityToolkit.Maui;
using MetarPulse.Maui.Services;
using Microsoft.Extensions.Logging;

namespace MetarPulse.Maui;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

        builder.Services.AddMauiBlazorWebView();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        // ─── API base URL ───────────────────────────────────────────────────
        // DEBUG  → emülatör/simülatör local adresleri
        // RELEASE → Cloudflare Tunnel production URL
#if DEBUG
        // TODO: ngrok URL'ini buraya yaz → ngrok http 5000 komutunun çıktısından al
        var apiBaseUrl = "https://gulflike-yosef-unsequenced.ngrok-free.dev";
#else
        var apiBaseUrl = "https://api.metarpulse.senindomain.com/";
#endif

        var hubUrl = $"{apiBaseUrl.TrimEnd('/')}/hubs/metar";

        // ─── Auth (Singleton — token lifecycle tüm uygulama boyunca) ────────
        builder.Services.AddSingleton<AuthService>(sp =>
        {
            var http = new HttpClient { BaseAddress = new Uri(apiBaseUrl) };
            // ngrok ücretsiz plan: browser warning sayfasını atla
            http.DefaultRequestHeaders.Add("ngrok-skip-browser-warning", "true");
            return new AuthService(http);
        });

        // ─── AuthHeaderHandler (Transient) ───────────────────────────────────
        builder.Services.AddTransient<AuthHeaderHandler>();

        // ─── ApiService HttpClient — Bearer token otomatik eklenir ──────────
        builder.Services.AddHttpClient<ApiService>(client =>
        {
            client.BaseAddress = new Uri(apiBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Add("Accept", "application/json");
            // ngrok ücretsiz plan: browser warning sayfasını atla
            client.DefaultRequestHeaders.Add("ngrok-skip-browser-warning", "true");
        }).AddHttpMessageHandler<AuthHeaderHandler>();

        // ─── SignalR istemcisi (Singleton) ────────────────────────────────────
        builder.Services.AddSingleton<SignalRService>(sp =>
        {
            var auth = sp.GetRequiredService<AuthService>();
            return new SignalRService(auth, hubUrl);
        });

        // ─── Tema yönetimi (Scoped — IJSRuntime Scoped olduğundan) ───────────
        builder.Services.AddScoped<ThemeService>();

        // ─── FCM token kaydı (Singleton) ─────────────────────────────────────
        builder.Services.AddSingleton<FcmTokenService>();

        // ─── Bookmark paylaşılan durum (Singleton) ───────────────────────────
        builder.Services.AddSingleton<BookmarkStateService>();

        // ─── METAR okunma durumu (Scoped — IJSRuntime'a bağlı) ───────────────
        builder.Services.AddScoped<ReadStateService>();

        return builder.Build();
    }
}
