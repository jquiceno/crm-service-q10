using Api.Attributes;
using Microsoft.AspNetCore.Mvc.Filters;
using Shared.Application.Interfaces;

namespace Api.Filters;

public sealed class ValidateRequestFilter : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var hasAttribute = context.ActionDescriptor.EndpointMetadata.OfType<ValidateRequestAttribute>().Any();

        if (!hasAttribute)
        {
            await next();
            return;
        }

        var services = context.HttpContext.RequestServices;

        foreach (var (_, value) in context.ActionArguments)
        {
            if (value is null || IsSimpleType(value.GetType()))
                continue;

            var validatorType = typeof(IRequestValidator<>).MakeGenericType(value.GetType());
            if (services.GetService(validatorType) is not IRequestValidator validator)
                continue;

            var result = await validator.ValidateAsync(value, context.HttpContext.RequestAborted);
            if (result.IsFailure)
            {
                context.Result = new ValidationErrorResult(result.Error);
                return;
            }
        }

        await next();
    }

    private static bool IsSimpleType(Type type) => type.IsPrimitive || type == typeof(string) || type == typeof(Guid) || type == typeof(DateTime);
}