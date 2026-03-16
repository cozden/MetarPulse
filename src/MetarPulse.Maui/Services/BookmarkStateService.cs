namespace MetarPulse.Maui.Services;

/// <summary>
/// Bookmark listesini bellekte tutar; sayfalar arası anlık senkronizasyon sağlar.
/// Singleton olarak kayıtlıdır.
/// </summary>
public class BookmarkStateService
{
    private List<string> _icaos = [];

    public IReadOnlyList<string> Icaos => _icaos;

    /// <summary>Herhangi bir bookmark değişikliğinde tetiklenir.</summary>
    public event Action? OnChanged;

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
