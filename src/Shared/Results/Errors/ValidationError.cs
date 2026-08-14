namespace Shared.Results.Errors;

public sealed record ValidationError : DomainError
{
    public string Property { get; init; } = string.Empty;
    public object? Value { get; init; }
    public IReadOnlyDictionary<string, object?>? Attributes { get; init; }
    public IReadOnlyList<ValidationError>? Children { get; init; }

    public ValidationError(string message, ErrorType type) : base(message, type) { }
}
