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

        var details = result.Errors
            .Select(e => new Error(e.ErrorCode, e.ErrorMessage, ErrorType.Validation)
            {
                Context = e.CustomState as IReadOnlyDictionary<string, object?>
            })
            .ToList();

        return ApplicationErrors.ValidationFailed(details);
    }

    async Task<Result> IRequestValidator.ValidateAsync(object input, CancellationToken cancellationToken)
        => await ValidateAsync((T)input, cancellationToken);
}