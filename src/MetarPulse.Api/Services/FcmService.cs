using FirebaseAdmin.Messaging;

namespace MetarPulse.Api.Services;

/// <summary>
/// Firebase Cloud Messaging servisi — uygulama kapalıyken push bildirim gönderir.
/// FirebaseApp.Create() Program.cs'de bir kez çağrılmalı.
/// </summary>
public class FcmService
{
    private readonly ILogger<FcmService> _logger;

    public FcmService(ILogger<FcmService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Birden fazla token'a aynı bildirimi gönderir.
    /// Geçersiz/süresi dolmuş token'ları döner (DB'den temizlemek için).
    /// </summary>
    public async Task<IReadOnlyList<string>> SendMulticastAsync(
        IReadOnlyList<string> tokens,
        string title,
        string body,
        Dictionary<string, string>? data = null,
        CancellationToken ct = default)
    {
        if (tokens.Count == 0) return [];

        var invalidTokens = new List<string>();

        // FCM multicast max 500 token — büyük listeler için parçala
        foreach (var chunk in tokens.Chunk(500))
        {
            var message = new MulticastMessage
            {
                Tokens = [.. chunk],
                Notification = new Notification { Title = title, Body = body },
                Data = data ?? new Dictionary<string, string>(),
                Android = new AndroidConfig
                {
                    Priority = Priority.High,
                    Notification = new AndroidNotification
                    {
                        Sound = "default",
                        ChannelId = "metar_alerts"
                    }
                },
                Apns = new ApnsConfig
                {
                    Aps = new Aps
                    {
                        Sound = "default",
                        Badge = 1
                    }
                }
            };

            try
            {
                var response = await FirebaseMessaging.DefaultInstance
                    .SendEachForMulticastAsync(message, ct);

                _logger.LogDebug("FCM multicast: {Success}/{Total} başarılı.",
                    response.SuccessCount, chunk.Length);

                // Başarısız token'ları topla (geçersiz/kayıtlı değil)
                for (var i = 0; i < response.Responses.Count; i++)
                {
                    var r = response.Responses[i];
                    if (!r.IsSuccess
                        && r.Exception?.MessagingErrorCode
                            is MessagingErrorCode.Unregistered
                            or MessagingErrorCode.InvalidArgument)
                    {
                        invalidTokens.Add(chunk[i]);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "FCM multicast chunk gönderilemedi.");
            }
        }

        return invalidTokens;
    }
}
