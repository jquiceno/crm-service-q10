using System.Net;
using System.Text.Json;
using Api.Responses;
using Shared.Domain;

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
                "An unexpected error occurred.", ErrorType.Internal.ToString().ToLowerInvariant());
        }
    }

    private async Task WriteErrorResponseAsync(
        HttpContext httpContext,
        HttpStatusCode httpStatusCode,
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
            new ErrorDto(message, type, []),
            statusCode);

        httpContext.Response.StatusCode = statusCode;
        httpContext.Response.ContentType = "application/json";

        await httpContext.Response.WriteAsJsonAsync(response, JsonOptions);
    }
}
