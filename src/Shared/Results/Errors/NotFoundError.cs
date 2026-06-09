namespace Shared.Results.Errors;

public sealed record NotFoundError : DomainError
{
    public NotFoundError(string message)
        : base(message, ErrorType.NotFound) { }
}
