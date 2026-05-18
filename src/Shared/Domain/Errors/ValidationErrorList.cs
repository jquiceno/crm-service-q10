namespace Shared.Domain.Errors;

public sealed record ValidationErrorList : DomainError
{
    public IReadOnlyList<ValidationError> Errors { get; }

    public ValidationErrorList(IReadOnlyList<ValidationError> errors)
        : base("Validation failed.", ErrorType.Validation)
    {
        Errors = errors;
    }

    public static implicit operator ValidationErrorList(List<ValidationError> errors) => new((IReadOnlyList<ValidationError>)errors);
}
