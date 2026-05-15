using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.OutputCaching;

namespace Api.Filters;

/// <summary>
/// Evicts all output-cache entries tagged with <paramref name="tag"/> after a
/// successful mutation (no exception, status code &lt; 400).
///
/// Use in tandem with <c>[OutputCache(Tags = new[] { "..." })]</c> on the
/// corresponding GET endpoints.
///
/// Example:
///   [HttpPost]
///   [OutputCacheInvalidate("weather-forecasts")]
///   public Task&lt;IActionResult&gt; Create(...) { ... }
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class OutputCacheInvalidateAttribute(string tag) : Attribute, IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var executed = await next();

        if (executed.Exception is not null)
            return;

        var statusCode = (executed.Result as IStatusCodeActionResult)?.StatusCode ?? 200;
        if (statusCode >= 400)
            return;

        var store = context.HttpContext.RequestServices.GetService<IOutputCacheStore>();
        if (store is null)
            return;

        await store.EvictByTagAsync(tag, context.HttpContext.RequestAborted);
    }
}
