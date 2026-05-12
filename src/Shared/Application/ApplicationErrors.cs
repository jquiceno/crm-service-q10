using Shared.Domain;

namespace Shared.Application;

public static class ApplicationErrors
{
    public static DomainError ValidationFailed(IReadOnlyList<ValidationError> errors, string context, string origin)
    {
        var details = DomainError.BuildDetails(errors);

        var dominantType = errors.Select(e => e.Type).Distinct().Count() == 1
            ? errors[0].Type
            : ErrorType.Validation;

        var message = errors.Count == 1
            ? errors[0].Message
            : dominantType switch
            {
                _ => "One or more validation errors occurred."
            };

        return new DomainError(message, dominantType)
        {
            Context = context,
            Origin = origin,
            Details = details
        };
    }
}
