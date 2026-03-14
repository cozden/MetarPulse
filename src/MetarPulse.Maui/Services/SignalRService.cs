using Microsoft.AspNetCore.SignalR.Client;

namespace MetarPulse.Maui.Services;

/// <summary>
/// MetarHub SignalR istemcisi.
/// Bağlantı durumu değişikliklerini ve gelen METAR/Alert mesajlarını yayar.
/// Bağlantı koptuğunda otomatik yeniden bağlanır (exponential backoff).
/// </summary>
public class SignalRService : IAsyncDisposable
{
    private readonly AuthService _auth;
    private readonly string _hubUrl;

    private HubConnection? _connection;

    // ── Olaylar ───────────────────────────────────────────────────────────────
    public event Action<string, object?>? OnMetarReceived;   // (icao, payload)
    public event Action<object?>? OnAlertReceived;           // alert payload
    public event Action<bool>? OnConnectionStateChanged;     // true=connected

    public bool IsConnected =>
        _connection?.State == HubConnectionState.Connected;

    public SignalRService(AuthService auth, string hubUrl)
    {
        _auth   = auth;
        _hubUrl = hubUrl;
    }

    // ── Bağlantı ─────────────────────────────────────────────────────────────

    public async Task ConnectAsync(CancellationToken ct = default)
    {
        if (_connection is not null) return;

        _connection = new HubConnectionBuilder()
            .WithUrl(_hubUrl, options =>
            {
                // JWT token'ı query string üzerinden gönder (hub policy)
                options.AccessTokenProvider = async () =>
                    await _auth.GetAccessTokenAsync();
            })
            .WithAutomaticReconnect(new[]
            {
                TimeSpan.FromSeconds(2),
                TimeSpan.FromSeconds(5),
                TimeSpan.FromSeconds(10),
                TimeSpan.FromSeconds(30)
            })
            .Build();

        // Event handler'lar
        _connection.On<object>("ReceiveMetar", payload =>
        {
            // Payload'dan StationId çek
            var icao = ExtractStationId(payload);
            OnMetarReceived?.Invoke(icao, payload);
        });

        _connection.On<object>("ReceiveAlert", payload =>
        {
            OnAlertReceived?.Invoke(payload);
        });

        _connection.Reconnecting  += _ => { OnConnectionStateChanged?.Invoke(false); return Task.CompletedTask; };
        _connection.Reconnected   += _ => { OnConnectionStateChanged?.Invoke(true);  return Task.CompletedTask; };
        _connection.Closed        += _ => { OnConnectionStateChanged?.Invoke(false); return Task.CompletedTask; };

        try
        {
            await _connection.StartAsync(ct);
            OnConnectionStateChanged?.Invoke(true);

            // Kullanıcı grubuna katıl (bildirimler için)
            if (_auth.CurrentUserId is not null)
                await _connection.InvokeAsync("JoinUserGroupAsync", _auth.CurrentUserId, ct);
        }
        catch
        {
            OnConnectionStateChanged?.Invoke(false);
        }
    }

    // ── İstasyon takibi ───────────────────────────────────────────────────────

    public async Task JoinStationAsync(string icao)
    {
        if (!IsConnected) return;
        await _connection!.InvokeAsync("JoinStationAsync", icao.ToUpperInvariant());
    }

    public async Task LeaveStationAsync(string icao)
    {
        if (!IsConnected) return;
        await _connection!.InvokeAsync("LeaveStationAsync", icao.ToUpperInvariant());
    }

    // ── Temizlik ─────────────────────────────────────────────────────────────

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync();
            _connection = null;
        }
    }

    // ── Yardımcı ─────────────────────────────────────────────────────────────

    private static string ExtractStationId(object payload)
    {
        try
        {
            if (payload is System.Text.Json.JsonElement el)
            {
                if (el.TryGetProperty("stationId", out var p)) return p.GetString() ?? "";
                if (el.TryGetProperty("StationId", out var p2)) return p2.GetString() ?? "";
            }
        }
        catch { }
        return "";
    }
}
