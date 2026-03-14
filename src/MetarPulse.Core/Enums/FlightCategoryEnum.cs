namespace MetarPulse.Core.Enums;

public enum FlightCategory
{
    Unknown = 0,
    LIFR = 1,   // Low IFR  — Ceiling < 500ft OR Visibility < 1SM
    IFR = 2,    // IFR      — Ceiling 500-999ft OR Visibility 1-3SM
    MVFR = 3,   // Marginal — Ceiling 1000-3000ft OR Visibility 3-5SM
    VFR = 4     // VFR      — Ceiling > 3000ft AND Visibility > 5SM
}
