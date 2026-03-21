namespace MetarPulse.Api.Services;

/// <summary>
/// Çalışma zamanı sistem ayarları — singleton.
/// Bakım modu ve uptime yönetimi.
/// </summary>
public class SystemSettingsService
{
    private readonly DateTime _startedAt = DateTime.UtcNow;

    public bool MaintenanceMode { get; set; } = false;

    public TimeSpan Uptime => DateTime.UtcNow - _startedAt;

    public DateTime StartedAt => _startedAt;
}
