namespace Shared.Domain;

public sealed record Error
{
    public static readonly Error None = new(string.Empty, string.Empty, ErrorType.None);

    public string Code { get; }
    public string Message { get; }
    public ErrorType Type { get; }
    public IReadOnlyList<Error> Details { get; init; } = [];

    public IReadOnlyDictionary<string, object?>? Context { get; init; }

    public Error(string code, string message, ErrorType type)
    {
        Code = code;
        Message = message;
        Type = type;
    }

    public bool Equals(Error? other) =>
        other is not null && Code == other.Code && Message == other.Message && Type == other.Type;

    public override int GetHashCode() => HashCode.Combine(Code, Message, Type);
}
