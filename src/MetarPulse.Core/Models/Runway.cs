namespace MetarPulse.Core.Models;

public class Runway
{
    public int Id { get; set; }
    public string AirportIdent { get; set; } = string.Empty;   // FK → Airport.Ident
    public int? LengthFt { get; set; }
    public int? WidthFt { get; set; }
    public string? Surface { get; set; }
    public bool IsLighted { get; set; }
    public bool IsClosed { get; set; }

    // Düşük numaralı uç (Low End)
    public string? LeIdent { get; set; }            // e.g. "16L"
    public double? LeHeadingDegT { get; set; }      // True heading

    // Yüksek numaralı uç (High End)
    public string? HeIdent { get; set; }            // e.g. "34R"
    public double? HeHeadingDegT { get; set; }      // True heading

    // Navigation property
    public Airport Airport { get; set; } = null!;
}
