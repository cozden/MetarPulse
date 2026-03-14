using MetarPulse.Core.Enums;

namespace MetarPulse.Core.Models;

public class CloudLayer
{
    public CloudCoverage Coverage { get; set; }
    public int AltitudeFt { get; set; }             // Taban yüksekliği (ft AGL)
    public CloudType Type { get; set; } = CloudType.None;
}
