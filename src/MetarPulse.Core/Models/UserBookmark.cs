namespace MetarPulse.Core.Models;

public class UserBookmark
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string StationIcao { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Airport? Airport { get; set; }
}
