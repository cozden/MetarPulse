using MetarPulse.Core.Enums;

namespace MetarPulse.Core.Models;

public class WeatherCondition
{
    public WeatherIntensity Intensity { get; set; } = WeatherIntensity.Moderate;
    public WeatherDescriptor Descriptor { get; set; } = WeatherDescriptor.None;
    public List<WeatherPhenomenon> Phenomena { get; set; } = new();

    public override string ToString()
    {
        var parts = new List<string>();
        if (Intensity == WeatherIntensity.Light) parts.Add("-");
        else if (Intensity == WeatherIntensity.Heavy) parts.Add("+");
        else if (Intensity == WeatherIntensity.Vicinity) parts.Add("VC");

        if (Descriptor != WeatherDescriptor.None) parts.Add(Descriptor.ToString());
        parts.AddRange(Phenomena.Select(p => p.ToString()));

        return string.Concat(parts);
    }
}
