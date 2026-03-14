using MetarPulse.Core.Enums;
using Microsoft.AspNetCore.Identity;

namespace MetarPulse.Core.Models;

public class ApplicationUser : IdentityUser
{
    public string? DisplayName { get; set; }
    public string? AvatarUrl { get; set; }
    public string PreferredLanguage { get; set; } = "tr";
    public string PreferredUnits { get; set; } = "metric";      // metric / imperial
    public string TimeZoneId { get; set; } = "Europe/Istanbul";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }
    public bool IsOnboardingCompleted { get; set; } = false;
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }

    // Navigation properties
    public PilotProfile? PilotProfile { get; set; }
    public ICollection<UserBookmark> Bookmarks { get; set; } = new List<UserBookmark>();
    public ICollection<NotificationPreference> NotificationPreferences { get; set; } = new List<NotificationPreference>();
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}
