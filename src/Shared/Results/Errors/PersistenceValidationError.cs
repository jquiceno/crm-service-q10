namespace Shared.Results.Errors;

public sealed record PersistenceValidationError : DomainError
{
    public PersistenceValidationError(string message)
        : base(message, ErrorType.Validation) { }
}
