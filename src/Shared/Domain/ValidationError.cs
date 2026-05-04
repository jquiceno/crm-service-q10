namespace Shared.Domain;

public sealed record ValidationError : DomainError
{
    public string Property { get; init; } = string.Empty;
    public IReadOnlyDictionary<string, object?>? Details { get; init; }

    public ValidationError(string message, ErrorType type) : base(message, type) { }
}
