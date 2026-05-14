using System.Diagnostics;
using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using WeatherForecast.Domain.Exceptions;

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
        catch (DomainException ex)
        {
            logger.LogWarning(ex, "Domain exception: {Message}", ex.Message);
            await WriteProblemDetailsAsync(httpContext, HttpStatusCode.BadRequest, ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception");
            await WriteProblemDetailsAsync(httpContext, HttpStatusCode.InternalServerError, "An unexpected error occurred.");
        }
    }

    private async Task WriteProblemDetailsAsync(
        HttpContext httpContext,
        HttpStatusCode statusCode,
        string detail)
    {
        if (httpContext.Response.HasStarted)
        {
            logger.LogWarning(
                "Cannot write ProblemDetails response. Response has already started for {Path}",
                httpContext.Request.Path);
            return;
        }

        var problemDetails = new ProblemDetails
        {
            Type = statusCode == HttpStatusCode.BadRequest
                ? "https://tools.ietf.org/html/rfc9110#section-15.5.1"
                : "https://tools.ietf.org/html/rfc9110#section-15.6.1",
            Status = (int)statusCode,
            Title = statusCode switch
            {
                HttpStatusCode.BadRequest => "Bad Request",
                _ => "Internal Server Error"
            },
            Detail = detail,
            Instance = httpContext.Request.Path,
            Extensions =
            {
                ["traceId"] = Activity.Current?.Id ?? httpContext.TraceIdentifier
            }
        };

        httpContext.Response.StatusCode = (int)statusCode;
        httpContext.Response.ContentType = "application/problem+json";

        await httpContext.Response.WriteAsJsonAsync(problemDetails, JsonOptions);
    }
}
