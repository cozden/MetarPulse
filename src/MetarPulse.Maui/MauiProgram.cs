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
        var apiBaseUrl = DeviceInfo.Platform == DevicePlatform.Android
            ? "http://10.0.2.2:5000/"
            : "http://localhost:5000/";
#else
        var apiBaseUrl = "https://api.metarpulse.senindomain.com/";
#endif

        var hubUrl = $"{apiBaseUrl.TrimEnd('/')}/hubs/metar";

        // ─── Auth (Singleton — token lifecycle tüm uygulama boyunca) ────────
        builder.Services.AddSingleton<AuthService>(sp =>
        {
            var http = new HttpClient { BaseAddress = new Uri(apiBaseUrl) };
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
        }).AddHttpMessageHandler<AuthHeaderHandler>();

        // ─── SignalR istemcisi (Singleton) ────────────────────────────────────
        builder.Services.AddSingleton<SignalRService>(sp =>
        {
            var auth = sp.GetRequiredService<AuthService>();
            return new SignalRService(auth, hubUrl);
        });

        // ─── Tema yönetimi (Scoped — IJSRuntime Scoped olduğundan) ───────────
        builder.Services.AddScoped<ThemeService>();

        return builder.Build();
    }
}
