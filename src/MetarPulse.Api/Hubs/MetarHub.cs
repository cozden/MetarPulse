using Microsoft.AspNetCore.SignalR;

namespace MetarPulse.Api.Hubs;

/// <summary>
/// Gerçek zamanlı METAR/TAF push bildirimleri için SignalR Hub.
/// Client bir istasyonu takip etmek için JoinStationAsync çağırır,
/// server yeni METAR geldiğinde ReceiveMetar ile push yapar.
/// </summary>
public class MetarHub : Hub
{
    private readonly ILogger<MetarHub> _logger;

    public MetarHub(ILogger<MetarHub> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// İstasyonun güncellemelerini almak için gruba katıl.
    /// </summary>
    public async Task JoinStationAsync(string icao)
    {
        var group = StationGroup(icao);
        await Groups.AddToGroupAsync(Context.ConnectionId, group);
        _logger.LogDebug("Client {Id} → {Group} grubuna katıldı.", Context.ConnectionId, group);
    }

    /// <summary>
    /// İstasyon grubundan ayrıl.
    /// </summary>
    public async Task LeaveStationAsync(string icao)
    {
        var group = StationGroup(icao);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, group);
        _logger.LogDebug("Client {Id} → {Group} grubundan ayrıldı.", Context.ConnectionId, group);
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogDebug("MetarHub bağlantısı: {Id}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogDebug("MetarHub bağlantısı kapandı: {Id}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>Kullanıcıya özel bildirim grubu — bildirim push için kullanılır.</summary>
    public async Task JoinUserGroupAsync(string userId)
    {
        var group = UserGroup(userId);
        await Groups.AddToGroupAsync(Context.ConnectionId, group);
        _logger.LogDebug("Client {Id} → {Group} kullanıcı grubuna katıldı.", Context.ConnectionId, group);
    }

    public static string StationGroup(string icao)
        => $"station_{icao.ToUpperInvariant()}";

    public static string UserGroup(string userId)
        => $"user_{userId}";
}
