using System.Diagnostics;
using System.Net;
using System.Text.Json;
using Api.Responses;

namespace Api.Middleware;

public sealed class GlobalExceptionMiddleware(
    RequestDelegate next,
    ILogger<GlobalExceptionMiddleware> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task InvokeAsync(HttpContext httpContext)
    {
        try
        {
            await next(httpContext);
        }
        catch (OperationCanceledException) when (httpContext.RequestAborted.IsCancellationRequested)
        {
            httpContext.Response.StatusCode = 499;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception");
            await WriteErrorResponseAsync(httpContext, HttpStatusCode.InternalServerError,
                "INTERNAL_ERROR", "An unexpected error occurred.", "internal");
        }
    }

    private async Task WriteErrorResponseAsync(
        HttpContext httpContext,
        HttpStatusCode httpStatusCode,
        string code,
        string message,
        string type)
    {
        if (httpContext.Response.HasStarted)
        {
            logger.LogWarning(
                "Cannot write error response. Response has already started for {Path}",
                httpContext.Request.Path);
            return;
        }

        var statusCode = (int)httpStatusCode;

        var response = new ApiErrorResponse(
            new ErrorDto(code, message, type, []),
            statusCode);

        httpContext.Response.StatusCode = statusCode;
        httpContext.Response.ContentType = "application/json";

        await httpContext.Response.WriteAsJsonAsync(response, JsonOptions);
    }
}
