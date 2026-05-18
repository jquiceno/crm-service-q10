namespace Shared.Domain.Errors;

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

    public static DomainError FromValidationDomainErrors(IReadOnlyList<ValidationError> errors) =>
        new("Domain validation failed.", ErrorType.DomainError) { Details = BuildDetails(errors) };

    public static IReadOnlyList<ErrorDetail> BuildDetails(IReadOnlyList<ValidationError> errors) =>
        errors
            .GroupBy(e => e.Property)
            .Select(g =>
            {
                var children = BuildChildren(g);
                return new ErrorDetail(
                    g.Key,
                    children is null ? g.Select(e => e.Message).ToList() : null,
                    g.FirstOrDefault(e => e.Attributes is not null)?.Attributes,
                    g.FirstOrDefault(e => e.Value is not null)?.Value,
                    children);
            })
            .ToList();

    private static IReadOnlyList<ErrorDetail>? BuildChildren(IEnumerable<ValidationError> group)
    {
        var children = group
            .Where(e => e.Children is { Count: > 0 })
            .SelectMany(e => e.Children!)
            .ToList();
        return children.Count == 0 ? null : BuildDetails(children);
    }

    public virtual bool Equals(DomainError? other) =>
        other is not null && Message == other.Message && Type == other.Type;

    public override int GetHashCode() => HashCode.Combine(Message, Type);
}

public sealed record ErrorDetail(
    string Property,
    IReadOnlyList<string>? Errors,
    IReadOnlyDictionary<string, object?>? Attributes = null,
    object? Value = null,
    IReadOnlyList<ErrorDetail>? Children = null);
