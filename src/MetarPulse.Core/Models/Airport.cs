namespace MetarPulse.Core.Models;

public class Airport
{
    public int Id { get; set; }
    public string Ident { get; set; } = string.Empty;          // ICAO kodu (e.g. "LTFM")
    public string Type { get; set; } = string.Empty;           // large_airport, medium_airport, small_airport
    public string Name { get; set; } = string.Empty;
    public double LatitudeDeg { get; set; }
    public double LongitudeDeg { get; set; }
    public int? ElevationFt { get; set; }
    public string IsoCountry { get; set; } = string.Empty;     // "TR", "US", etc.
    public string? Municipality { get; set; }
    public string? IataCode { get; set; }
    public double? MagneticVariation { get; set; }             // True → Magnetic dönüşüm için
    public DateTime LastSynced { get; set; }

    // Navigation properties
    public ICollection<Runway> Runways { get; set; } = new List<Runway>();
    public ICollection<UserBookmark> Bookmarks { get; set; } = new List<UserBookmark>();
}
