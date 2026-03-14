namespace MetarPulse.Core.Enums;

public enum WeatherIntensity
{
    Light,      // -
    Moderate,   // (no prefix)
    Heavy,      // +
    Vicinity    // VC
}

public enum WeatherDescriptor
{
    None,
    MI, // Shallow
    BC, // Patches
    PR, // Partial
    DR, // Low Drifting
    BL, // Blowing
    SH, // Showers
    TS, // Thunderstorm
    FZ  // Freezing
}

public enum WeatherPhenomenon
{
    // Precipitation
    RA,     // Rain
    SN,     // Snow
    DZ,     // Drizzle
    SG,     // Snow Grains
    IC,     // Ice Crystals
    PL,     // Ice Pellets
    GR,     // Hail
    GS,     // Small Hail

    // Obscuration
    FG,     // Fog
    BR,     // Mist
    HZ,     // Haze
    FU,     // Smoke
    VA,     // Volcanic Ash
    DU,     // Widespread Dust
    SA,     // Sand
    PY,     // Spray

    // Other
    SQ,     // Squall
    FC,     // Funnel Cloud / Tornado
    SS,     // Sandstorm
    DS      // Duststorm
}
