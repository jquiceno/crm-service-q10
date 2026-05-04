using Shared.Application;
using Shared.Application.Interfaces;
using Shared.Domain;

namespace Infrastructure.Validation.FluentValidation;

public sealed class FluentRequestValidationAdapter<T>(IStructuralValidator<T> validator) : IRequestValidator<T>
{
    public async Task<Result> ValidateAsync(T input, CancellationToken cancellationToken = default)
    {
        var result = await validator.ValidateAsync(input, cancellationToken);

        if (result.IsValid)
            return Result.Success();

        var errors = result.Errors
            .Select(e =>
            {
                var staticError = e.CustomState as ValidationError;
                return new ValidationError(e.ErrorMessage, staticError?.Type ?? ErrorType.Validation)
                {
                    Property = e.PropertyName,
                    Details = staticError?.Details
                };
            })
            .ToList();

        return ApplicationErrors.ValidationFailed(errors, context: string.Empty, origin: string.Empty);
    }

    async Task<Result> IRequestValidator.ValidateAsync(object input, CancellationToken cancellationToken)
        => await ValidateAsync((T)input, cancellationToken);
}
