using System.Collections.ObjectModel;

namespace Shared.Result.Errors;

public sealed record ValidationErrorList : DomainError
{
    public IReadOnlyList<ValidationError> Errors { get; }

    public ValidationErrorList(IReadOnlyList<ValidationError> errors)
        : base("Validation failed.", ErrorType.Validation)
    {
        Errors = errors;
    }

    public static implicit operator ValidationErrorList(ReadOnlyCollection<ValidationError> errors) => new((IReadOnlyList<ValidationError>)errors);
}
