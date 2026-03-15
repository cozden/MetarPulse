using MetarPulse.Core.Enums;

namespace MetarPulse.Core.Models;

public class Metar
{
    public int Id { get; set; }
    public string RawText { get; set; } = string.Empty;
    public string StationId { get; set; } = string.Empty;      // ICAO kodu
    public DateTime ObservationTime { get; set; }               // UTC

    // Rüzgar
    public int WindDirection { get; set; }                      // Derece (magnetic)
    public int WindSpeed { get; set; }                          // Knot
    public int? WindGust { get; set; }                          // Knot (nullable)
    public bool IsVariableWind { get; set; }
    public int? VariableWindFrom { get; set; }
    public int? VariableWindTo { get; set; }

    // Görüş
    public int VisibilityMeters { get; set; }
    public bool IsCavok { get; set; }
    public string? RvrRaw { get; set; }                         // RVR ham değer

    // Bulut
    public List<CloudLayer> CloudLayers { get; set; } = new();

    // Hava durumu olayları
    public List<WeatherCondition> WeatherConditions { get; set; } = new();

    // Sıcaklık & Basınç
    public int? Temperature { get; set; }                       // °C
    public int? DewPoint { get; set; }                          // °C
    public decimal? AltimeterHpa { get; set; }                  // QNH hPa
    public decimal? AltimeterInHg { get; set; }                 // QNH inHg

    // Trend
    public string? Trend { get; set; }                          // NOSIG, BECMG, TEMPO

    // Hesaplanan değerler
    public FlightCategory Category { get; set; } = FlightCategory.Unknown;
    public int CeilingFeet { get; set; }                        // BKN/OVC en düşük

    // Meta
    public DateTime FetchedAt { get; set; } = DateTime.UtcNow;
    public string? SourceProvider { get; set; }                 // "MGM_RASAT", "AVWX" vb.
    public bool IsSpeci { get; set; }                           // SPECI mi?

    // Navigation
    public Airport? Airport { get; set; }
}
