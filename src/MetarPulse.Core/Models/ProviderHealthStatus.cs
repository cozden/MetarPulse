namespace MetarPulse.Core.Models;

public class ProviderHealthStatus
{
    public string ProviderName { get; set; } = string.Empty;
    public bool IsHealthy { get; set; }
    public DateTime LastChecked { get; set; }
    public DateTime? LastSuccess { get; set; }
    public string? LastError { get; set; }
    public int ConsecutiveFailures { get; set; }
    public bool IsCircuitOpen { get; set; }
}
