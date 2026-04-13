using Infrastructure.Cache;
using Infrastructure.Settings;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.Options;
using Shared.Application.Interfaces;

namespace Api.Filters;

/// <summary>
/// Invalidates all cache keys whose prefix matches <paramref name="routePrefix"/> after a
/// successful mutation (non-4xx/5xx response, no unhandled exception).
///
/// The route prefix must match the path used by the corresponding cached GET endpoint,
/// relative to the server root (e.g. "api/v1/weather-forecasts").
///
/// Example:
///   [HttpPost]
///   [InvalidateCache("api/v1/weather-forecasts")]
///   public async Task&lt;IActionResult&gt; Create(...) { ... }
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class InvalidateCacheAttribute : Attribute, IAsyncActionFilter
{
    private readonly string _routePrefix;

    public InvalidateCacheAttribute(string routePrefix)
    {
        _routePrefix = routePrefix;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var executed = await next();

        // Skip invalidation when the handler threw or returned an error response.
        if (executed.Exception is not null)
            return;

        var statusCode = (executed.Result as IStatusCodeActionResult)?.StatusCode ?? 200;
        if (statusCode >= 400)
            return;

        var services = context.HttpContext.RequestServices;
        var cache = services.GetRequiredService<ICacheService>();
        var settings = services.GetRequiredService<IOptions<CacheSettings>>().Value;

        var prefix = CacheKeyBuilder.BuildRoutePrefix(settings.KeyPrefix, _routePrefix);
        await cache.RemoveByPrefixAsync(prefix, context.HttpContext.RequestAborted);
    }
}
