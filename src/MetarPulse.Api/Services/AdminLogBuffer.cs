using System.Collections.Concurrent;

namespace MetarPulse.Api.Services;

/// <summary>
/// Son 1000 log satırını tutan in-memory circular buffer.
/// AdminLogBufferSink tarafından doldurulur, AdminController tarafından okunur.
/// </summary>
public class AdminLogBuffer
{
    private const int MaxCapacity = 1000;

    private readonly ConcurrentQueue<LogEntry> _entries = new();

    public void Add(LogEntry entry)
    {
        _entries.Enqueue(entry);
        while (_entries.Count > MaxCapacity)
            _entries.TryDequeue(out _);
    }

    /// <summary>Ters kronolojik sırada (en yeni önce) filtrelenmiş kayıtları döner.</summary>
    public IEnumerable<LogEntry> Query(
        string? level = null,
        DateTime? from = null,
        DateTime? to = null,
        string? source = null,
        int limit = 200)
    {
        var query = _entries.AsEnumerable().Reverse();

        if (!string.IsNullOrWhiteSpace(level))
            query = query.Where(e => e.Level.Equals(level, StringComparison.OrdinalIgnoreCase));

        if (from.HasValue)
            query = query.Where(e => e.Timestamp >= from.Value);

        if (to.HasValue)
            query = query.Where(e => e.Timestamp <= to.Value);

        if (!string.IsNullOrWhiteSpace(source))
            query = query.Where(e => e.Source != null &&
                e.Source.Contains(source, StringComparison.OrdinalIgnoreCase));

        return query.Take(limit);
    }

    public void Clear() => _entries.Clear();
}

public record LogEntry(
    DateTime Timestamp,
    string Level,       // "INF", "WRN", "ERR", "DBG"
    string Message,
    string? Source,     // SourceContext
    string? Exception);
