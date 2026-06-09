using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Shared.Application;
using Shared.Application.Ports;
using Shared.Presentation.Attributes;
using Shared.Presentation.Filters;
using System.Text.Json;

namespace Api.Filters;

public sealed class ValidateRequestFilter : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (!context.ModelState.IsValid)
        {
            var hasJsonErrors = context.ModelState.Keys.Any(k => k.StartsWith('$'));

            if (hasJsonErrors)
            {
                var bodyParam = context.ActionDescriptor.Parameters
                    .FirstOrDefault(p => p.BindingInfo?.BindingSource == BindingSource.Body);

                if (bodyParam != null && context.HttpContext.Request.Body.CanSeek)
                {
                    context.HttpContext.Request.Body.Position = 0;
                    try
                    {
                        using var doc = await JsonDocument.ParseAsync(
                            context.HttpContext.Request.Body,
                            cancellationToken: context.HttpContext.RequestAborted).ConfigureAwait(false);
                        var scanned = JsonTypeScanner.Scan(doc.RootElement, bodyParam.ParameterType);
                        if (scanned.Count > 0)
                        {
                            context.Result = new ValidationErrorResult(
                                ApplicationErrors.ValidationFailed(scanned, context: string.Empty, origin: string.Empty));
                            return;
                        }
                    }
                    catch (Exception ex) when (ex is JsonException or IOException or NotSupportedException) { }
                }
            }

            var errors = ModelStateValidationAdapter.Build(context.ModelState);
            context.Result = new ValidationErrorResult(
                ApplicationErrors.ValidationFailed(errors, context: string.Empty, origin: string.Empty));
            return;
        }

        var hasAttribute = context.ActionDescriptor.EndpointMetadata.OfType<ValidateRequestAttribute>().Any();

        if (!hasAttribute)
        {
            await next().ConfigureAwait(false);
            return;
        }

        var services = context.HttpContext.RequestServices;

        foreach (var (_, value) in context.ActionArguments)
        {
            if (value is null || IsSimpleType(value.GetType()))
                continue;

            var validatorType = typeof(IRequestValidatorPort<>).MakeGenericType(value.GetType());
            if (services.GetService(validatorType) is not IRequestValidatorPort validator)
                continue;

            var result = await validator.ValidateAsync(value, context.HttpContext.RequestAborted).ConfigureAwait(false);
            if (result.IsFailure)
            {
                context.Result = new ValidationErrorResult(result.Error);
                return;
            }
        }

        await next().ConfigureAwait(false);
    }

    private static bool IsSimpleType(Type type) => type.IsPrimitive || type == typeof(string) || type == typeof(Guid) || type == typeof(DateTime);
}
