namespace MetarPulse.Core.Models;

public class Taf
{
    public int Id { get; set; }
    public string RawText { get; set; } = string.Empty;
    public string StationId { get; set; } = string.Empty;
    public DateTime IssueTime { get; set; }
    public DateTime ValidFrom { get; set; }
    public DateTime ValidTo { get; set; }

    public List<TafPeriod> Periods { get; set; } = new();

    public DateTime FetchedAt { get; set; } = DateTime.UtcNow;
    public string? SourceProvider { get; set; }
}

public class TafPeriod
{
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public string ChangeIndicator { get; set; } = string.Empty; // FM, BECMG, TEMPO, PROB30/40
    public int? Probability { get; set; }

    // Rüzgar
    public int? WindDirection { get; set; }
    public int? WindSpeed { get; set; }
    public int? WindGust { get; set; }

    // Görüş
    public int? VisibilityMeters { get; set; }

    // Bulut
    public List<CloudLayer> CloudLayers { get; set; } = new();

    // Hava durumu
    public List<WeatherCondition> WeatherConditions { get; set; } = new();
}
