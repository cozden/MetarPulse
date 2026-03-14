namespace MetarPulse.Core.Models;

public class NotificationPreference
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string StationIcao { get; set; } = string.Empty;

    // Zaman filtresi
    public List<DayOfWeek> ActiveDays { get; set; } = new();
    public TimeOnly StartTime { get; set; } = new TimeOnly(6, 0);
    public TimeOnly EndTime { get; set; } = new TimeOnly(22, 0);
    public string TimeZoneId { get; set; } = "Europe/Istanbul";

    // İçerik filtresi
    public bool NotifyOnEveryMetar { get; set; } = false;
    public bool NotifyOnCategoryChange { get; set; } = true;
    public bool NotifyOnSpeci { get; set; } = true;
    public bool NotifyOnVfrAchieved { get; set; } = true;
    public bool NotifyOnSignificantWeather { get; set; } = true;

    // Gelişmiş eşikler
    public int? VisibilityThresholdMeters { get; set; }
    public int? CeilingThresholdFeet { get; set; }
    public int? WindThresholdKnots { get; set; }
}
