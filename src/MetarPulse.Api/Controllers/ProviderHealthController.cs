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

    /// <summary>GET /api/providers — Provider listesi ve öncelikleri</summary>
    [HttpGet]
    public IActionResult GetProviders()
    {
        var chain = _manager.GetWeatherProviderChain("LTFM"); // Örnek
        return Ok(chain.Select(p => new
        {
            p.ProviderName,
            p.Priority,
            p.IsEnabled
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
}
