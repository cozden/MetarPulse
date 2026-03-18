using MetarPulse.Core.Enums;

namespace MetarPulse.Infrastructure.Providers.Notam;

/// <summary>
/// Q-kodu subject ve traffic kodlarından VFR etkisini hesaplar.
/// Yeni provider ekleme durumunda da aynı sınıf kullanılır.
/// </summary>
public static class NotamVfrClassifier
{
    // Subject → VfrImpact eşlemeleri (ICAO Q-kod tablosu bazlı)
    private static readonly Dictionary<string, NotamVfrImpact> SubjectMap = new(StringComparer.OrdinalIgnoreCase)
    {
        // Warning — pist, taxiway, engel, büyük airspace
        { "RW", NotamVfrImpact.Warning },   // Runway
        { "WY", NotamVfrImpact.Warning },   // Taxiway
        { "OB", NotamVfrImpact.Warning },   // Obstacles
        { "OL", NotamVfrImpact.Warning },   // Obstacle lights
        { "OM", NotamVfrImpact.Warning },   // Obstacle marking
        { "AA", NotamVfrImpact.Warning },   // Minimum altitude
        { "AC", NotamVfrImpact.Warning },   // Control zone
        { "AD", NotamVfrImpact.Warning },   // Air defence zone
        { "AE", NotamVfrImpact.Warning },   // Restricted area
        { "AG", NotamVfrImpact.Warning },   // Danger area
        { "AH", NotamVfrImpact.Warning },   // Upper airspace
        { "AL", NotamVfrImpact.Warning },   // Min altitude altimeter
        { "AN", NotamVfrImpact.Warning },   // Noise restriction
        { "AP", NotamVfrImpact.Warning },   // Entry prohibited
        { "AQ", NotamVfrImpact.Warning },   // Prohibited area
        { "AR", NotamVfrImpact.Warning },   // Air refuelling area
        { "AT", NotamVfrImpact.Warning },   // Terminal control area
        { "AU", NotamVfrImpact.Warning },   // Upper advisory area
        { "AV", NotamVfrImpact.Warning },   // Volcanic ash
        { "AW", NotamVfrImpact.Warning },   // Airway
        { "AX", NotamVfrImpact.Warning },   // Danger area boundary
        { "AZ", NotamVfrImpact.Warning },   // Special aviation area

        // Warning — hava meydanı kısıtlaması
        { "FA", NotamVfrImpact.Warning },   // Aerodrome
        { "FB", NotamVfrImpact.Warning },   // Braking action
        { "FC", NotamVfrImpact.Warning },   // Clearway
        { "FD", NotamVfrImpact.Warning },   // Declared distances
        { "FE", NotamVfrImpact.Warning },   // Threshold
        { "FF", NotamVfrImpact.Warning },   // FATO (helipad)
        { "FG", NotamVfrImpact.Warning },   // Glide path
        { "FH", NotamVfrImpact.Warning },   // Helipad
        { "FI", NotamVfrImpact.Warning },   // Instrument approach
        { "FJ", NotamVfrImpact.Warning },   // Approach cat
        { "FK", NotamVfrImpact.Warning },   // CAT II/III
        { "FL", NotamVfrImpact.Warning },   // Slop indicator
        { "FM", NotamVfrImpact.Warning },   // Aerodrome movement area
        { "FN", NotamVfrImpact.Warning },   // Apron
        { "FO", NotamVfrImpact.Warning },   // Stopway
        { "FP", NotamVfrImpact.Warning },   // Parking area
        { "FQ", NotamVfrImpact.Warning },   // Runway surface
        { "FR", NotamVfrImpact.Warning },   // RESA
        { "FS", NotamVfrImpact.Warning },   // Shoulder
        { "FT", NotamVfrImpact.Warning },   // Touchdown zone
        { "FU", NotamVfrImpact.Warning },   // Road
        { "FW", NotamVfrImpact.Warning },   // Wind direction indicator

        // Caution — aydınlatma, hizmet kısmi
        { "LA", NotamVfrImpact.Caution },   // Approach lighting
        { "LB", NotamVfrImpact.Caution },   // Runway centerline
        { "LC", NotamVfrImpact.Caution },   // Runway edge lights
        { "LD", NotamVfrImpact.Caution },   // Landing direction lights
        { "LE", NotamVfrImpact.Caution },   // Runway end lights
        { "LF", NotamVfrImpact.Caution },   // Sequenced flash lights
        { "LG", NotamVfrImpact.Caution },   // Pilot activated lighting
        { "LH", NotamVfrImpact.Caution },   // High intensity runway lights
        { "LI", NotamVfrImpact.Caution },   // Inner marker
        { "LJ", NotamVfrImpact.Caution },   // Runway alignment indicator
        { "LK", NotamVfrImpact.Caution },   // CAT II/III lighting
        { "LL", NotamVfrImpact.Caution },   // Low intensity runway lights
        { "LM", NotamVfrImpact.Caution },   // Middle marker
        { "LN", NotamVfrImpact.Caution },   // Terminal NDB lights
        { "LP", NotamVfrImpact.Caution },   // PAPI
        { "LR", NotamVfrImpact.Caution },   // All landing area lights
        { "LS", NotamVfrImpact.Caution },   // Stopway lights
        { "LT", NotamVfrImpact.Caution },   // Threshold lights
        { "LV", NotamVfrImpact.Caution },   // VASIS
        { "LW", NotamVfrImpact.Caution },   // Helipad lights
        { "LX", NotamVfrImpact.Caution },   // Taxiway lights
        { "WA", NotamVfrImpact.Caution },   // Air display
        { "WB", NotamVfrImpact.Caution },   // Blasting operation
        { "WC", NotamVfrImpact.Caution },   // Bird hazard
        { "WD", NotamVfrImpact.Caution },   // Demolition
        { "WE", NotamVfrImpact.Caution },   // Exercises
        { "WF", NotamVfrImpact.Caution },   // Air refuelling
        { "WG", NotamVfrImpact.Caution },   // Glider operations
        { "WH", NotamVfrImpact.Caution },   // Helo operations
        { "WJ", NotamVfrImpact.Caution },   // Jet operations
        { "WL", NotamVfrImpact.Caution },   // Laser
        { "WM", NotamVfrImpact.Caution },   // Missile/rocket
        { "WO", NotamVfrImpact.Caution },   // Overflight restrictions
        { "WP", NotamVfrImpact.Caution },   // Parachute ops
        { "WR", NotamVfrImpact.Caution },   // Radioactive/toxic
        { "WS", NotamVfrImpact.Caution },   // Significant weather
        { "WT", NotamVfrImpact.Caution },   // Tethered aircraft
        { "WU", NotamVfrImpact.Caution },   // Unmanned aircraft
        { "WV", NotamVfrImpact.Caution },   // Formation flight
        { "WW", NotamVfrImpact.Caution },   // Significant met warning
        { "WZ", NotamVfrImpact.Caution },   // Other airspace

        // Advisory — nav, com, hizmet
        { "NA", NotamVfrImpact.Advisory },  // All nav
        { "NB", NotamVfrImpact.Advisory },  // NDB
        { "NC", NotamVfrImpact.Advisory },  // DVOR
        { "ND", NotamVfrImpact.Advisory },  // DME
        { "NF", NotamVfrImpact.Advisory },  // Fan marker
        { "NG", NotamVfrImpact.Advisory },  // Glide path
        { "NH", NotamVfrImpact.Advisory },  // HF comm
        { "NI", NotamVfrImpact.Advisory },  // ILS
        { "NJ", NotamVfrImpact.Advisory },  // NAVAIDS (outer)
        { "NK", NotamVfrImpact.Advisory },  // MLS
        { "NL", NotamVfrImpact.Advisory },  // Locator
        { "NM", NotamVfrImpact.Advisory },  // VOR/DME
        { "NN", NotamVfrImpact.Advisory },  // TACAN
        { "NO", NotamVfrImpact.Advisory },  // VOR
        { "NP", NotamVfrImpact.Advisory },  // GNSS
        { "NQ", NotamVfrImpact.Advisory },  // GNSS channel
        { "NR", NotamVfrImpact.Advisory },  // ADF
        { "NS", NotamVfrImpact.Advisory },  // DVOR/DME
        { "NT", NotamVfrImpact.Advisory },  // TACAN
        { "NU", NotamVfrImpact.Advisory },  // NDB/DME
        { "NV", NotamVfrImpact.Advisory },  // VOR
        { "NX", NotamVfrImpact.Advisory },  // Other nav
        { "CA", NotamVfrImpact.Advisory },  // Air/ground
        { "CB", NotamVfrImpact.Advisory },  // VOLMET
        { "CC", NotamVfrImpact.Advisory },  // SELCAL
        { "CD", NotamVfrImpact.Advisory },  // Controller-pilot data
        { "CE", NotamVfrImpact.Advisory },  // En-route comm
        { "CF", NotamVfrImpact.Advisory },  // UHF
        { "CG", NotamVfrImpact.Advisory },  // VHF
        { "CH", NotamVfrImpact.Advisory },  // HF
        { "CL", NotamVfrImpact.Advisory },  // LLWAS
        { "CM", NotamVfrImpact.Advisory },  // Meteorological service
        { "CN", NotamVfrImpact.Advisory },  // ATIS
        { "CO", NotamVfrImpact.Advisory },  // Oceanic ATC
        { "CP", NotamVfrImpact.Advisory },  // Pilot-activated comm
        { "CR", NotamVfrImpact.Advisory },  // ATS route
        { "CS", NotamVfrImpact.Advisory },  // Secondary surveillance radar
        { "CT", NotamVfrImpact.Advisory },  // Terminal VHF
    };

    /// <summary>
    /// Q-kodu subject ve traffic kodlarından VFR etkisini hesaplar.
    /// IFR-only NOTAM'lar VFR etkisi taşımaz (Advisory'ye düşürülür).
    /// </summary>
    public static NotamVfrImpact Classify(string subject, NotamTraffic traffic, NotamScope scope)
    {
        // Sadece IFR trafiği etkileyen NOTAM'lar VFR'yi etkilemez
        if (traffic == NotamTraffic.Ifr && scope == NotamScope.EnRoute)
            return NotamVfrImpact.None;

        if (string.IsNullOrWhiteSpace(subject) || subject.Length < 2)
            return NotamVfrImpact.Advisory;

        var key = subject[..2].ToUpperInvariant();

        if (SubjectMap.TryGetValue(key, out var impact))
        {
            // IFR-only ise en fazla Advisory
            if (traffic == NotamTraffic.Ifr && impact > NotamVfrImpact.Advisory)
                return NotamVfrImpact.Advisory;

            return impact;
        }

        return NotamVfrImpact.Advisory;
    }
}
