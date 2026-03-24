using Api.Settings;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Api.Controllers;

/// <summary>
/// Health check endpoints without dependency on the database or other external services, providing basic liveness information about the application.
/// </summary>
[ApiController]
[Route("api/v1/health")]
public sealed class HealthController(IOptions<AppInfoSettings> appInfoOptions) : ControllerBase
{
    [HttpGet("live")]
    public IActionResult GetLive()
    {
        var appInfo = appInfoOptions.Value;

        return Ok(new
        {
            status = "ok",
            serviceName = appInfo.ServiceName,
            version = appInfo.Version
        });
    }
}
