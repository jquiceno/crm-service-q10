namespace Shared.Results.Errors;

public sealed record InternalError : DomainError
{
    public InternalError(string message)
        : base(message, ErrorType.Internal) { }
}
