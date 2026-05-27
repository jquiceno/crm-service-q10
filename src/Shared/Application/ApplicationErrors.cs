using Shared.Result.Errors;

namespace Shared.Application;

public static class ApplicationErrors
{
    public static DomainError ValidationFailed(
        IReadOnlyList<ValidationError> errors,
        string context,
        string origin) =>
        new("Validation failed", ErrorType.Validation)
        {
            Context = context,
            Origin = origin,
            Details = DomainError.BuildDetails(errors)
        };
}
