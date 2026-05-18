using System.Net;
using System.Text.Json;
using Api.Mapping;
using Api.Responses;
using Microsoft.AspNetCore.Diagnostics;
using Shared.Domain.Errors;

namespace Api.Middleware;

internal sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            httpContext.Response.StatusCode = 499;
            return true;
        }

        logger.LogError(exception, "Unhandled exception");

        if (httpContext.Response.HasStarted)
        {
            logger.LogWarning("Cannot write error response — response already started for {Path}",
                httpContext.Request.Path);
            return false;
        }

        const int statusCode = (int)HttpStatusCode.InternalServerError;
        httpContext.Response.StatusCode = statusCode;
        httpContext.Response.ContentType = "application/json";

        await httpContext.Response.WriteAsJsonAsync(
            new ApiErrorResponse(
                new ErrorDto(
                    ErrorHttpMapper.ToErrorTypeName(ErrorType.Internal),
                    ErrorHttpMapper.ToErrorCode(ErrorType.Internal),
                    "An unexpected error occurred.",
                    []),
                statusCode),
            JsonSerializerOptions.Web,
            cancellationToken);

        return true;
    }
}
