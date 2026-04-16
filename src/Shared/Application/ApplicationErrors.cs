using Shared.Domain;

namespace Shared.Application;

public static class ApplicationErrors
{
    public static Error ValidationFailed(IReadOnlyList<Error> details) =>
        new("VALIDATION_FAILED", "One or more validation errors occurred.", ErrorType.Validation)
        {
            Details = details
        };
}
