namespace Shared.Results.Errors;

public sealed record ConflictError : DomainError
{
    public ConflictError(string message)
        : base(message, ErrorType.Conflict) { }
}
