using Api.Results;
using Infrastructure.Settings;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Api.Controllers;

/// <summary>
/// Health check endpoints without dependency on the database or other external services, providing basic liveness information about the application.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
public sealed class HealthController(IOptions<AppInfoSettings> appInfoOptions) : ControllerBase
{
    [HttpGet("info")]
    public HttpOkResult<object> GetInfo()
    {
        var appInfo = appInfoOptions.Value;

        return new HttpOkResult<object>(new
        {
            status = "ok",
            serviceName = appInfo.ServiceName,
            version = appInfo.Version
        });
    }
}