namespace MetarPulse.Core.Models;

/// <summary>
/// Admin panelinden yapılan provider ayar değişikliklerini saklar.
/// Null alanlar "override yok, appsettings.json kullan" anlamına gelir.
/// </summary>
public class ProviderSettingOverride
{
    public int Id { get; set; }

    /// <summary>Provider adı — "AVWX", "CheckWX", "MGM_RASAT" vb.</summary>
    public string ProviderName { get; set; } = string.Empty;

    public bool?   Enabled                    { get; set; }
    public int?    Priority                   { get; set; }
    public string? BaseUrl                    { get; set; }
    public string? ApiKey                     { get; set; }
    public int?    TimeoutSeconds             { get; set; }
    public int?    RetryCount                 { get; set; }
    public int?    CircuitBreakerThreshold    { get; set; }
    public int?    CircuitBreakerDurationSeconds { get; set; }

    public DateTime UpdatedAt  { get; set; } = DateTime.UtcNow;
    public string?  UpdatedBy  { get; set; }
}
