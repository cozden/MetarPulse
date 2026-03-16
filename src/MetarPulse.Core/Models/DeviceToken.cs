namespace MetarPulse.Core.Models;

/// <summary>
/// Kullanıcıya ait FCM (Firebase Cloud Messaging) cihaz token'ı.
/// Her cihaz login sonrası token'ını kaydeder — uygulama kapalıyken push bildirim için.
/// </summary>
public class DeviceToken
{
    public int Id { get; set; }

    /// <summary>Token sahibi kullanıcı (FK → AspNetUsers).</summary>
    public string UserId { get; set; } = default!;

    /// <summary>FCM registration token — cihaz başına benzersiz.</summary>
    public string Token { get; set; } = default!;

    /// <summary>Cihaz platformu: "android" | "ios".</summary>
    public string Platform { get; set; } = default!;

    /// <summary>Son güncelleme (upsert ile tutulur).</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ApplicationUser User { get; set; } = default!;
}
