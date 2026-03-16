using MetarPulse.Maui.Models;

namespace MetarPulse.Maui.Services;

/// <summary>
/// Bookmark listesini ve son bilinen METAR verilerini bellekte tutar.
/// Sayfalar arası anlık senkronizasyon + navigasyon sonrası anlık gösterim sağlar.
/// Singleton olarak kayıtlıdır.
/// </summary>
public class BookmarkStateService
{
    private List<string> _icaos = [];

    /// <summary>Son başarılı API çağrısından gelen METAR listesi (cache).</summary>
    public List<MetarViewModel> CachedMetars { get; private set; } = [];

    public IReadOnlyList<string> Icaos => _icaos;

    /// <summary>Herhangi bir bookmark değişikliğinde tetiklenir.</summary>
    public event Action? OnChanged;

    /// <summary>METAR cache'ini günceller (skeleton göstermeden hızlı yükleme için).</summary>
    public void SetMetarCache(List<MetarViewModel> metars) => CachedMetars = metars;

    public void Set(List<string> icaos)
    {
        _icaos = icaos;
        OnChanged?.Invoke();
    }

    public void Add(string icao)
    {
        if (!_icaos.Contains(icao, StringComparer.OrdinalIgnoreCase))
        {
            _icaos = [.._icaos, icao.ToUpperInvariant()];
            OnChanged?.Invoke();
        }
    }

    public void Remove(string icao)
    {
        var before = _icaos.Count;
        _icaos = _icaos.Where(i => !i.Equals(icao, StringComparison.OrdinalIgnoreCase)).ToList();
        if (_icaos.Count != before) OnChanged?.Invoke();
    }
}
