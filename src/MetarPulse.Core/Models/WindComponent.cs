namespace MetarPulse.Core.Models;

public class WindComponent
{
    public double HeadwindKnots { get; set; }
    public double CrosswindKnots { get; set; }
    public double TailwindKnots { get; set; }
    public bool IsTailwind => TailwindKnots > 0;

    public string RunwayIdent { get; set; } = string.Empty;
    public double RunwayHeadingMagnetic { get; set; }
}
