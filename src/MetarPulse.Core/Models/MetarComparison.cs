using MetarPulse.Core.Enums;

namespace MetarPulse.Core.Models;

public class MetarComparison
{
    public Metar OldMetar { get; set; } = null!;
    public Metar NewMetar { get; set; } = null!;

    public bool CategoryChanged { get; set; }
    public FlightCategory OldCategory { get; set; }
    public FlightCategory NewCategory { get; set; }

    public bool WindChanged { get; set; }
    public bool VisibilityChanged { get; set; }
    public bool CeilingChanged { get; set; }
    public bool SignificantWeatherChanged { get; set; }

    public bool IsImproving { get; set; }
    public bool IsDeteriorating { get; set; }

    public List<string> ChangeSummary { get; set; } = new();
}
