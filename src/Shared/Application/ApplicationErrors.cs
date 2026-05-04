using Shared.Domain;

namespace Shared.Application;

public static class ApplicationErrors
{
    public static DomainError ValidationFailed(IReadOnlyList<ValidationError> errors, string context, string origin)
    {
        var attributes = errors
            .GroupBy(e => e.Property)
            .Select(g => new ErrorAttribute(
                g.Key,
                g.Select(e => e.Message).ToList(),
                g.FirstOrDefault(e => e.Details is not null)?.Details))
            .ToList();

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
            Attributes = attributes
        };
    }
}
