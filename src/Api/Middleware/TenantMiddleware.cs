using System.Text.Json;
using Api.Session;
using Infrastructure.MasterAccess.Http.Tenants;
using Shared.Application.Ports;
using Shared.Presentation.Mapping;
using Shared.Presentation.Responses;
using Shared.Results.Errors;

namespace Api.Middleware;

/// <summary>
/// Resolves the tenant for the current request from the <c>EntityCode</c> query parameter or the
/// <c>X-Entity-Code</c> header, writing the connection string through
/// <see cref="ITenantConnectionInitializer"/> so the per-tenant <c>DbContext</c> connects to the right
/// database. Registered only when multitenancy is enabled.
/// </summary>
public sealed class TenantMiddleware(RequestDelegate next, ILoggerPort<TenantMiddleware> logger)
{
    // Infrastructure endpoints that must answer without a tenant.
    private static readonly string[] ExcludedPaths = ["/health", "/openapi", "/scalar"];

    public async Task InvokeAsync(
        HttpContext context,
        ITenantInfoClient tenantInfoClient,
        ITenantConnectionInitializer session)
    {
        var path = context.Request.Path.Value ?? string.Empty;

        if (HttpMethods.IsOptions(context.Request.Method)
            || ExcludedPaths.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        var entityCode = context.Request.Query["EntityCode"].FirstOrDefault()
            ?? context.Request.Headers["X-Entity-Code"].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(entityCode))
        {
            await WriteErrorAsync(
                context,
                new ValidationError(
                    "A tenant code is required as the 'EntityCode' query parameter or the 'X-Entity-Code' header.",
                    ErrorType.Validation) { Property = "EntityCode" }).ConfigureAwait(false);
            return;
        }

        var result = await tenantInfoClient
            .GetByCodeAsync(entityCode, context.RequestAborted)
            .ConfigureAwait(false);

        if (result.IsFailure)
        {
            await WriteErrorAsync(context, result.Error).ConfigureAwait(false);
            return;
        }

        logger.Debug("Resolved tenant {EntityCode} to database {DbName}",
            result.Value.EntityCode, result.Value.DbName);

        session.Initialize(result.Value.ConnectionString);

        await next(context).ConfigureAwait(false);
    }

    private static Task WriteErrorAsync(HttpContext context, DomainError error)
    {
        var statusCode = (int)ErrorHttpMapper.ToHttpStatusCode(error.Type);
        context.Response.StatusCode = statusCode;

        var body = new ApiErrorResponse(
            new ErrorDto(
                ErrorHttpMapper.ToErrorTypeName(error.Type),
                ErrorHttpMapper.ToErrorCode(error.Type),
                error.Message,
                ErrorHttpMapper.ToErrorDetailDtos(error.Details)),
            statusCode);

        return context.Response.WriteAsJsonAsync(body, JsonSerializerOptions.Web, context.RequestAborted);
    }
}
