using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Shared.Presentation.Mapping;
using Shared.Presentation.Responses;
using System.Net;
using System.Text.Json;
using Shared.Results.Errors;

namespace Shared.Presentation.Middleware;

[SuppressMessage(
    "Performance",
    "CA1812:AvoidUninstantiatedInternalClasses",
    Justification = "Instantiated by ASP.NET Core when registered via AddExceptionHandler<T>().")]
public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(exception);

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
            cancellationToken).ConfigureAwait(false);

        return true;
    }
}
