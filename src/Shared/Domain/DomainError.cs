namespace Shared.Domain;

public record DomainError
{
    public static readonly DomainError None = new(string.Empty, ErrorType.None);

    public string Message { get; }
    public ErrorType Type { get; }
    public string Context { get; init; } = string.Empty;
    public string Origin { get; init; } = string.Empty;
    public IReadOnlyList<ErrorAttribute> Attributes { get; init; } = [];

    public DomainError(string message, ErrorType type)
    {
        Message = message;
        Type = type;
    }

    public virtual bool Equals(DomainError? other) =>
        other is not null && Message == other.Message && Type == other.Type;

    public override int GetHashCode() => HashCode.Combine(Message, Type);
}

public sealed record ErrorAttribute(
    string Property,
    IReadOnlyList<string> Messages,
    IReadOnlyDictionary<string, object?>? Details = null);
