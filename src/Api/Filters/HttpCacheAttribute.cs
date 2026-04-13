using System.Text.Json;
using Infrastructure.Cache;
using Infrastructure.Settings;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;
using Shared.Application.Interfaces;

namespace Api.Filters;

/// <summary>
/// Caches successful GET responses in the configured cache store.
/// Place this attribute on idempotent controller actions that return <see cref="OkObjectResult"/>.
///
/// Cache key format: {prefix}:{path}:{queryHash}:{tenant}:{locale}
///   prefix  — from CacheSettings.KeyPrefix (default "api:v1")
///   path    — the full request path, trimmed of leading slash
///   tenant  — X-Tenant-Id header or "tenant_id" JWT claim (or "_" if absent)
///   locale  — Accept-Language header (or "_" if absent)
///
/// Example:
///   [HttpGet]
///   [HttpCache(ttlSeconds: 60)]
///   public async Task&lt;IActionResult&gt; GetAll(...) { ... }
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class HttpCacheAttribute : Attribute, IAsyncActionFilter
{
    private readonly int _ttlSeconds;

    /// <param name="ttlSeconds">
    /// Per-endpoint TTL. 0 means use <see cref="CacheSettings.DefaultTtlSeconds"/>.
    /// </param>
    public HttpCacheAttribute(int ttlSeconds = 0)
    {
        _ttlSeconds = ttlSeconds;
    }

    /// <summary>
    /// Include the tenant identifier in the cache key.
    /// Set to false only for endpoints that return the same data for all tenants.
    /// Default: true.
    /// </summary>
    public bool VaryByTenant { get; init; } = true;

    /// <summary>
    /// Include the Accept-Language value in the cache key.
    /// Set to false for endpoints whose responses are locale-independent.
    /// Default: true.
    /// </summary>
    public bool VaryByLocale { get; init; } = true;

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (!HttpMethods.IsGet(context.HttpContext.Request.Method))
        {
            await next();
            return;
        }

        var services = context.HttpContext.RequestServices;
        var cache = services.GetRequiredService<ICacheService>();
        var settings = services.GetRequiredService<IOptions<CacheSettings>>().Value;

        var key = BuildKey(context, settings.KeyPrefix);

        var cached = await cache.GetAsync<JsonElement>(key, context.HttpContext.RequestAborted);

        if (cached.ValueKind != JsonValueKind.Undefined)
        {
            context.Result = new OkObjectResult(cached);
            return;
        }

        var executed = await next();

        if (executed.Result is OkObjectResult { Value: not null } ok)
        {
            var ttl = _ttlSeconds > 0
                ? TimeSpan.FromSeconds(_ttlSeconds)
                : TimeSpan.FromSeconds(settings.DefaultTtlSeconds);

            await cache.SetAsync(key, ok.Value, ttl, context.HttpContext.RequestAborted);
        }
    }

    private string BuildKey(ActionExecutingContext context, string prefix)
    {
        var request = context.HttpContext.Request;
        var route = request.Path.Value?.TrimStart('/') ?? string.Empty;
        var query = request.QueryString.Value;

        var tenant = VaryByTenant
            ? request.Headers["X-Tenant-Id"].FirstOrDefault()
              ?? context.HttpContext.User.FindFirst("tenant_id")?.Value
            : null;

        var locale = VaryByLocale
            ? request.Headers["Accept-Language"].FirstOrDefault()
            : null;

        return CacheKeyBuilder.Build(prefix, route, query, tenant, locale);
    }
}
