namespace Shared.Domain;

public record DomainError
{
    public static readonly DomainError None = new(string.Empty, ErrorType.None);

    public string Message { get; }
    public ErrorType Type { get; }
    public string Context { get; init; } = string.Empty;
    public string Origin { get; init; } = string.Empty;
    public IReadOnlyList<ErrorDetail> Details { get; init; } = [];

    public DomainError(string message, ErrorType type)
    {
        Message = message;
        Type = type;
    }

    public static DomainError FromValidationDomainErrors(IReadOnlyList<ValidationError> errors)
    {
        var details = errors
            .GroupBy(e => e.Property)
            .Select(g => new ErrorDetail(
                g.Key,
                g.Select(e => e.Message).ToList(),
                g.FirstOrDefault(e => e.Attributes is not null)?.Attributes,
                g.FirstOrDefault(e => e.Value is not null)?.Value))
            .ToList();
        return new DomainError("Domain validation failed.", ErrorType.DomainError) { Details = details };
    }
    
    public virtual bool Equals(DomainError? other) =>
        other is not null && Message == other.Message && Type == other.Type;

    public override int GetHashCode() => HashCode.Combine(Message, Type);
}

public sealed record ErrorDetail(
    string Property,
    IReadOnlyList<string> Errors,
    IReadOnlyDictionary<string, object?>? Attributes = null,
    object? Value = null);
