using System.Diagnostics;
using Infrastructure.Logging;
using Infrastructure.Observability;
using Shared.Application.Ports;

namespace Api.Middleware;

public sealed class RequestLoggingMiddleware(
    RequestDelegate next,
    ILoggerPort<RequestLoggingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext httpContext)
    {
        var startTimestamp = Stopwatch.GetTimestamp();

        // Enrich all pipeline logs with the partial http block (without status/latency).
        // The finally overwrites it with complete HttpRequestLogProperties via Serilog's LIFO.
        using var requestScope = BuildContextProperties(httpContext).PushHttpProperties();

        try
        {
            // Early capture: the TraceId is immutable for the whole request. It is written in
            // OnStarting (just before flush) so it survives a Response.Clear() from the
            // UseExceptionHandler and is set idempotently (indexer, not TryAdd).
            var traceId = Activity.Current?.TraceId.ToString();

            if (traceId is not null)
            {
                httpContext.Response.OnStarting(() =>
                {
                    httpContext.Response.Headers[TraceHeaders.TraceId] = traceId;
                    return Task.CompletedTask;
                });
            }

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
