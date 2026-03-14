namespace MetarPulse.Core.Enums;

public enum CloudCoverage
{
    SKC = 0,    // Sky Clear
    CLR = 1,    // Clear (automated)
    FEW = 2,    // Few (1-2 oktas)
    SCT = 3,    // Scattered (3-4 oktas)
    BKN = 4,    // Broken (5-7 oktas) — ceiling
    OVC = 5,    // Overcast (8 oktas) — ceiling
    VV = 6      // Vertical Visibility (obscured)
}

public enum CloudType
{
    None = 0,
    CB = 1,     // Cumulonimbus
    TCU = 2     // Towering Cumulus
}
