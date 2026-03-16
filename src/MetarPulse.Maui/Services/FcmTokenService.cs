using Microsoft.Extensions.Logging;
#if ANDROID
using Plugin.Firebase.CloudMessaging;
#endif

namespace MetarPulse.Maui.Services;

/// <summary>
/// FCM token'ını alır ve API'ye kaydeder.
/// Android dışı platformlarda no-op olarak çalışır.
/// </summary>
public class FcmTokenService
{
    private readonly ApiService _api;
    private readonly ILogger<FcmTokenService> _logger;

    public FcmTokenService(ApiService api, ILogger<FcmTokenService> logger)
    {
        _api = api;
        _logger = logger;
    }

    /// <summary>
    /// Geçerli FCM token'ını alıp API'ye kaydeder.
    /// Kullanıcı login olduktan sonra çağrılmalı.
    /// </summary>
    public async Task RegisterAsync(CancellationToken ct = default)
    {
#if ANDROID
        try
        {
            await CrossFirebaseCloudMessaging.Current.CheckIfValidAsync();
            var token = await CrossFirebaseCloudMessaging.Current.GetTokenAsync();

            if (!string.IsNullOrWhiteSpace(token))
            {
                await _api.RegisterDeviceTokenAsync(token, ct);
                _logger.LogInformation("FCM token API'ye kaydedildi.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "FCM token kaydedilemedi.");
        }
#else
        await Task.CompletedTask;
#endif
    }

    /// <summary>
    /// Logout sırasında token'ı API'den sil.
    /// </summary>
    public async Task UnregisterAsync(CancellationToken ct = default)
    {
#if ANDROID
        try
        {
            var token = await CrossFirebaseCloudMessaging.Current.GetTokenAsync();
            if (!string.IsNullOrWhiteSpace(token))
                await _api.UnregisterDeviceTokenAsync(token, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "FCM token silinemedi.");
        }
#else
        await Task.CompletedTask;
#endif
    }
}
