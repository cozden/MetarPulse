using MetarPulse.Abstractions.Providers;
using Microsoft.AspNetCore.Mvc;

namespace MetarPulse.Api.Controllers;

[ApiController]
[Route("api/providers")]
public class ProviderHealthController : ControllerBase
{
    private readonly IProviderManager _manager;

    public ProviderHealthController(IProviderManager manager)
    {
        _manager = manager;
    }

    /// <summary>GET /api/providers/health — Tüm provider'ların sağlık durumu</summary>
    [HttpGet("health")]
    public IActionResult GetHealth()
    {
        var statuses = _manager.GetHealthStatuses();
        return Ok(statuses.Select(s => new
        {
            s.ProviderName,
            s.IsHealthy,
            s.IsCircuitOpen,
            s.ConsecutiveFailures,
            LastChecked = s.LastChecked.ToString("O"),
            LastSuccess = s.LastSuccess?.ToString("O"),
            s.LastError
        }));
    }

    /// <summary>GET /api/providers — Tüm provider'lar, sıralı, sağlık durumuyla</summary>
    [HttpGet]
    public IActionResult GetProviders()
    {
        var all    = _manager.GetAllWeatherProviders();
        var health = _manager.GetHealthStatuses().ToDictionary(h => h.ProviderName);
        return Ok(all
            .OrderBy(p => p.Priority)
            .Select(p => new
            {
                p.ProviderName,
                p.Priority,
                p.IsEnabled,
                IsHealthy = health.TryGetValue(p.ProviderName, out var h) && h.IsHealthy
            }));
    }

    /// <summary>POST /api/providers/{name}/enable — Provider'ı etkinleştir/devre dışı bırak</summary>
    [HttpPost("{name}/enable")]
    public async Task<IActionResult> SetEnabled(
        string name,
        [FromQuery] bool enabled,
        CancellationToken ct)
    {
        try
        {
            await _manager.EnableProviderAsync(name, enabled);
            return Ok(new { message = $"{name} → enabled={enabled}" });
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>POST /api/providers/reorder — Fallback sırasını güncelle</summary>
    [HttpPost("reorder")]
    public async Task<IActionResult> Reorder([FromBody] ProviderOrderRequest req, CancellationToken ct)
    {
        await _manager.ReorderAsync(req.GlobalOrder, req.TurkeyOrder);
        return Ok();
    }

    public record ProviderOrderRequest(List<string> GlobalOrder, List<string> TurkeyOrder);
}
