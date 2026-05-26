using System.Diagnostics;
using Infrastructure.Logging;
using Shared.Application.Ports;

namespace Api.Middleware;

public sealed class RequestLoggingMiddleware(
    RequestDelegate next,
    ILoggerPort<RequestLoggingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        var startTimestamp = Stopwatch.GetTimestamp();

        // Enriquece todos los logs del pipeline con el bloque http parcial (sin status/latency).
        // El finally lo sobreescribe con HttpRequestLogProperties completo via LIFO de Serilog.
        using var requestScope = BuildContextProperties(httpContext).PushHttpProperties();

        try
        {
            await next(httpContext).ConfigureAwait(false);
        }
        finally
        {
            var elapsedMs = (long)Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;

            // null-safe: using(null) is a no-op in C# 8+
            using (BuildRequestLogProperties(httpContext, elapsedMs).PushHttpProperties())
            using (httpContext.GetLogProperties()?.PushProperties())
            {
                logger.Info("http.request.completed");
            }
        }
    }

    private static HttpContextProperties BuildContextProperties(HttpContext httpContext) =>
        new(
            UserAgent: httpContext.Request.Headers.UserAgent.ToString(),
            RemoteAddress: httpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty,
            Method: httpContext.Request.Method,
            Route: httpContext.Request.Path.Value ?? string.Empty
        );

    private static HttpRequestLogProperties BuildRequestLogProperties(HttpContext httpContext, long elapsedMs) =>
        new(
            UserAgent: httpContext.Request.Headers.UserAgent.ToString(),
            RemoteAddress: httpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty,
            Method: httpContext.Request.Method,
            Route: httpContext.Request.Path.Value ?? string.Empty,
            StatusCode: httpContext.Response.StatusCode,
            LatencyMs: elapsedMs
        );
}
