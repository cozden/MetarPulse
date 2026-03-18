namespace MetarPulse.Maui.Models;

public class NotamViewModel
{
    public string NotamId { get; set; } = string.Empty;
    public string AirportIdent { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Traffic { get; set; } = string.Empty;
    public string Scope { get; set; } = string.Empty;
    public string VfrImpact { get; set; } = "None";
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public bool IsPermanent { get; set; }
    public string? Schedule { get; set; }
    public string LowerLimit { get; set; } = string.Empty;
    public string UpperLimit { get; set; } = string.Empty;
    public string RawText { get; set; } = string.Empty;
    public string SourceProvider { get; set; } = string.Empty;

    // ── Hesaplanan özellikler ────────────────────────────────────────────────

    /// <summary>VFR etkisi için DaisyUI renk sınıfı.</summary>
    public string ImpactBadgeCss => VfrImpact switch
    {
        "Warning"  => "badge-error",
        "Caution"  => "badge-warning",
        "Advisory" => "badge-info",
        _          => "badge-ghost"
    };

    /// <summary>Geçerlilik süresi özet string'i.</summary>
    public string ValidityText
    {
        get
        {
            if (IsPermanent) return "PERM";
            var from = EffectiveFrom.ToUniversalTime().ToString("ddHHmm");
            var to = EffectiveTo?.ToUniversalTime().ToString("ddHHmm") ?? "PERM";
            return $"{from}Z – {to}Z";
        }
    }

    /// <summary>Şu an aktif mi?</summary>
    public bool IsActive => IsPermanent || EffectiveTo == null || EffectiveTo > DateTime.UtcNow;
}

public class NotamSummaryViewModel
{
    public string AirportIdent { get; set; } = string.Empty;
    public int ActiveCount { get; set; }
    public bool HasVfrWarning { get; set; }
    public bool HasVfrCaution { get; set; }

    /// <summary>Dashboard kartı için badge CSS sınıfı (en yüksek önem düzeyi).</summary>
    public string BadgeCss => HasVfrWarning ? "badge-error" :
                              HasVfrCaution ? "badge-warning" :
                              ActiveCount > 0 ? "badge-info" : string.Empty;

    public bool HasAny => ActiveCount > 0;
}
