using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;

namespace Stock.Api.Controllers;

[ApiController]
[Route("health")]
public sealed class HealthController(HealthCheckService healthChecks) : ControllerBase
{
    [HttpGet("live")]
    public IActionResult Live() => Ok(new { status = "live" });

    [HttpGet("ready")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Ready(CancellationToken cancellationToken)
    {
        var report = await healthChecks.CheckHealthAsync(check => check.Tags.Contains("ready"), cancellationToken);
        if (report.Status != HealthStatus.Healthy)
            return StatusCode(StatusCodes.Status503ServiceUnavailable);

        return Ok(new { status = "ready" });
    }
}
