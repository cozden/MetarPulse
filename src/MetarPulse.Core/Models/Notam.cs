using MetarPulse.Core.Enums;

namespace MetarPulse.Core.Models;

/// <summary>
/// NOTAM kaydı. Q-kodu ayrıştırılır, VFR etkisi hesaplanır.
/// Provider-agnostik — aviationweather.gov veya başka kaynaktan gelebilir.
/// </summary>
public class Notam
{
    public int Id { get; set; }

    // ── Kimlik ────────────────────────────────────────────────────────────────
    public string NotamId { get; set; } = string.Empty;     // ör. "A1234/26"
    public string AirportIdent { get; set; } = string.Empty; // ICAO
    public string FirIdent { get; set; } = string.Empty;     // ör. "LTAA"

    // ── Seri & Numara ─────────────────────────────────────────────────────────
    public char Series { get; set; }      // A, B, C...
    public int Number { get; set; }
    public int Year { get; set; }

    // ── Tip ───────────────────────────────────────────────────────────────────
    public NotamType NotamType { get; set; } = NotamType.New;

    // ── Q-kodu alanları ───────────────────────────────────────────────────────
    public string QLine { get; set; } = string.Empty;       // Ham Q satırı
    public string Subject { get; set; } = string.Empty;     // 2 harf, ör. "OB"
    public NotamTraffic Traffic { get; set; } = NotamTraffic.All;
    public NotamScope Scope { get; set; } = NotamScope.Aerodrome;

    // ── Koordinat & Yarıçap ───────────────────────────────────────────────────
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public int? RadiusNm { get; set; }

    // ── İrtifa ────────────────────────────────────────────────────────────────
    public string LowerLimit { get; set; } = string.Empty;  // ör. "SFC", "50FT AGL"
    public string UpperLimit { get; set; } = string.Empty;  // ör. "FL100", "UNL"

    // ── Süre ──────────────────────────────────────────────────────────────────
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }      // null = PERM
    public bool IsPermanent { get; set; }
    public bool IsEstimatedEnd { get; set; }
    public string? Schedule { get; set; }           // D-serisi

    // ── İçerik ────────────────────────────────────────────────────────────────
    public string RawText { get; set; } = string.Empty;

    // ── Hesaplanan ────────────────────────────────────────────────────────────
    public NotamVfrImpact VfrImpact { get; set; } = NotamVfrImpact.None;

    // ── Meta ──────────────────────────────────────────────────────────────────
    public DateTime IssueDate { get; set; }
    public DateTime FetchedAt { get; set; } = DateTime.UtcNow;
    public string SourceProvider { get; set; } = string.Empty;

    // ── Navigation ────────────────────────────────────────────────────────────
    public Airport? Airport { get; set; }

    // ── Hesaplanan özellikler (DB'ye yazılmaz) ────────────────────────────────

    /// <summary>Şu an aktif mi? (EffectiveTo null ise PERM = aktif)</summary>
    public bool IsActive => EffectiveTo == null || EffectiveTo > DateTime.UtcNow;
}
