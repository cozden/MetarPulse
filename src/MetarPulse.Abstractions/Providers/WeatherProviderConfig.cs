namespace MetarPulse.Abstractions.Providers;

public class WeatherProviderSettings
{
    public List<string> GlobalFallbackOrder { get; set; } = new();
    public Dictionary<string, RegionOverride> RegionOverrides { get; set; } = new();
    public Dictionary<string, ProviderConfig> Providers { get; set; } = new();
}

public class RegionOverride
{
    public List<string> IcaoPrefixes { get; set; } = new();
    public List<string> FallbackOrder { get; set; } = new();
    public string Description { get; set; } = string.Empty;
}

public class ProviderConfig
{
    public bool Enabled { get; set; } = true;
    public int Priority { get; set; }
    public string BaseUrl { get; set; } = string.Empty;
    public string? ApiKey { get; set; }
    public int TimeoutSeconds { get; set; } = 5;
    public int RetryCount { get; set; } = 2;
    public int CircuitBreakerThreshold { get; set; } = 5;
    public int CircuitBreakerDurationSeconds { get; set; } = 30;
}

public class AirportProviderSettings
{
    public string ActiveProvider { get; set; } = "OurAirports";
    public Dictionary<string, ProviderConfig> Providers { get; set; } = new();
}
