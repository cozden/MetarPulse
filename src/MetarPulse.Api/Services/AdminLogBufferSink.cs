using Serilog.Core;
using Serilog.Events;

namespace MetarPulse.Api.Services;

/// <summary>
/// Serilog sink — log olaylarını AdminLogBuffer'a yazar.
/// </summary>
public class AdminLogBufferSink : ILogEventSink
{
    private readonly AdminLogBuffer _buffer;

    public AdminLogBufferSink(AdminLogBuffer buffer)
    {
        _buffer = buffer;
    }

    public void Emit(LogEvent logEvent)
    {
        var level = logEvent.Level switch
        {
            LogEventLevel.Debug       => "DBG",
            LogEventLevel.Information => "INF",
            LogEventLevel.Warning     => "WRN",
            LogEventLevel.Error       => "ERR",
            LogEventLevel.Fatal       => "FTL",
            _                         => "VRB"
        };

        logEvent.Properties.TryGetValue("SourceContext", out var sourceCtx);
        var source = sourceCtx?.ToString().Trim('"');

        // Kısa kaynak adı: "MetarPulse.Api.Controllers.MetarController" → "MetarController"
        if (source != null)
        {
            var dot = source.LastIndexOf('.');
            if (dot >= 0) source = source[(dot + 1)..];
        }

        _buffer.Add(new LogEntry(
            Timestamp : logEvent.Timestamp.UtcDateTime,
            Level     : level,
            Message   : logEvent.RenderMessage(),
            Source    : source,
            Exception : logEvent.Exception?.Message));
    }
}
