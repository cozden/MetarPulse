namespace MetarPulse.Core.Enums;

/// <summary>VFR operasyonlarına etkisi — Q-kodu subject/traffic'ten hesaplanır.</summary>
public enum NotamVfrImpact
{
    None               = 0,  // VFR'yi etkilemiyor veya yalnızca IFR için
    Advisory           = 1,  // Bilgi amaçlı (NAV, COM vb.)
    Caution            = 2,  // Dikkat (aydınlatma, küçük kısıtlamalar)
    Warning            = 3,  // Uyarı (pist kapalı, engel, büyük airspace kısıtlaması)
    OperationsCritical = 4   // OPS KISITMASI — havaalanı kapalı, pist CLSD, IHA yasağı, tüm operasyonları durduran kısıtlama
}

/// <summary>NOTAM Q-kodundan parse edilen trafik tipi.</summary>
public enum NotamTraffic
{
    All = 0,  // IV veya bilinmiyor — her iki trafik tipini etkiler
    Vfr = 1,  // V — sadece VFR
    Ifr = 2,  // I — sadece IFR
    Checklist = 3  // K — OAT (askeri) trafik
}

/// <summary>NOTAM kapsam alanı.</summary>
public enum NotamScope
{
    Aerodrome = 0,  // A — hava meydanı
    EnRoute   = 1,  // E — parkur
    Nav       = 2,  // W — uyarı
    All       = 3   // AE veya bilinmiyor
}

/// <summary>NOTAM tipi.</summary>
public enum NotamType
{
    New     = 0,  // N
    Replace = 1,  // R
    Cancel  = 2   // C
}
