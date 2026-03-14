using MetarPulse.Core.Enums;

namespace MetarPulse.Core.Models;

public class PilotProfile
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;

    // Lisans
    public LicenseType? LicenseType { get; set; }
    public string? LicenseNumber { get; set; }
    public DateTime? LicenseExpiryDate { get; set; }

    // Deneyim
    public int? TotalFlightHours { get; set; }
    public string? AircraftTypeRatings { get; set; }    // "C172, PA28, B737"

    // Baz meydan
    public string? BaseAirportIcao { get; set; }
    public string? SecondaryAirportIcao { get; set; }

    // Kişisel limitler (crosswind uyarıları için)
    public int? PersonalCrosswindLimitKts { get; set; }
    public int? PersonalTailwindLimitKts { get; set; }
    public int? PersonalVisibilityMinMeters { get; set; }
    public int? PersonalCeilingMinFeet { get; set; }

    // Navigation
    public ApplicationUser User { get; set; } = null!;
    public Airport? BaseAirport { get; set; }
}
