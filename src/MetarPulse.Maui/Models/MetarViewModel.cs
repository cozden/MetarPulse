namespace MetarPulse.Maui.Models;

/// <summary>
/// UI model — API'den gelen MetarDto'yu yansıtır.
/// FlightCategory string olarak saklanır ("VFR", "MVFR", "IFR", "LIFR", "Unknown").
/// </summary>
public class MetarViewModel
{
    public string StationId { get; set; } = string.Empty;
    public string RawText { get; set; } = string.Empty;
    public DateTime ObservationTime { get; set; }

    // Rüzgar
    public int WindDirection { get; set; }
    public int WindSpeed { get; set; }
    public int? WindGust { get; set; }
    public bool IsVariableWind { get; set; }
    public int? VariableWindFrom { get; set; }
    public int? VariableWindTo { get; set; }

    // Görüş & Tavan
    public int VisibilityMeters { get; set; }
    public bool IsCavok { get; set; }
    public int CeilingFeet { get; set; }

    // Kategori
    public string Category { get; set; } = "Unknown";

    // Sıcaklık & Basınç
    public int? Temperature { get; set; }
    public int? DewPoint { get; set; }
    public decimal? AltimeterHpa { get; set; }
    public decimal? AltimeterInHg { get; set; }

    // Trend & Meta
    public string? Trend { get; set; }
    public bool IsSpeci { get; set; }
    public string? SourceProvider { get; set; }
    public DateTime FetchedAt { get; set; }

    // Koleksiyonlar
    public List<CloudLayerViewModel> CloudLayers { get; set; } = new();
    public List<string> WeatherConditions { get; set; } = new();

    // Bayat mı?
    public bool IsStale { get; set; }

    // ── Hesaplanan özellikler ──────────────────────────────────

    /// <summary>DaisyUI/Tailwind CSS sınıf adı (border-vfr, text-lifr vb.)</summary>
    public string CategoryCssClass => Category.ToLowerInvariant() switch
    {
        "vfr"  => "vfr",
        "mvfr" => "mvfr",
        "ifr"  => "ifr",
        "lifr" => "lifr",
        _      => "neutral"
    };

    /// <summary>Gözlem zamanının yerel string temsili (UTC+0 offset'siz).</summary>
    public string ObservationTimeLocal => ObservationTime.ToLocalTime().ToString("HH:mm");

    /// <summary>Kaç dakika önce çekildi.</summary>
    public int AgeMinutes => (int)(DateTime.UtcNow - ObservationTime).TotalMinutes;

    /// <summary>Rüzgar özet string'i (örn. "270/12kt G18" veya "VRB/05kt").</summary>
    public string WindSummary
    {
        get
        {
            var dir = IsVariableWind ? "VRB" : $"{WindDirection:D3}°";
            var spd = $"{WindSpeed}kt";
            var gust = WindGust.HasValue ? $" G{WindGust}kt" : "";
            return $"{dir}/{spd}{gust}";
        }
    }

    /// <summary>Görüş özeti — sadece asıl raporda CAVOK varsa CAVOK gösterir, aksi halde metre.</summary>
    public string VisibilitySummary => IsCavok ? "CAVOK" : $"{VisibilityMeters}m";

    /// <summary>QNH özeti — her ikisi de varsa hPa tercih edilir.</summary>
    public string QnhSummary =>
        AltimeterHpa.HasValue ? $"Q{AltimeterHpa:F0}" :
        AltimeterInHg.HasValue ? $"A{AltimeterInHg:F2}" : "—";

    /// <summary>Tavan özeti (ft).</summary>
    public string CeilingSummary => CeilingFeet >= 12000 ? "CLR" : $"{CeilingFeet:N0}ft";
}

public class CloudLayerViewModel
{
    public string Coverage { get; set; } = string.Empty;   // FEW, SCT, BKN, OVC
    public int AltitudeFt { get; set; }
    public string Type { get; set; } = string.Empty;       // CB, TCU, None
}
