namespace Shared.Domain;

public class Result
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public DomainError Error { get; }

    public static Result Success() => new(true, DomainError.None);
    public static Result Failure(DomainError error) => new(false, error);
    public static implicit operator Result(DomainError error) => Failure(error);

    protected Result(bool isSuccess, DomainError error)
    {
        if (isSuccess && !ReferenceEquals(error, DomainError.None))
            throw new InvalidOperationException("A successful result cannot have an error.");

        if (!isSuccess && ReferenceEquals(error, DomainError.None))
            throw new InvalidOperationException("A failed result must have an error.");

        IsSuccess = isSuccess;
        Error = error;
    }
}

public class Result<T> : Result
{
    private readonly T? _value;

    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("Cannot access Value of a failed result.");

    private Result(T? value, bool isSuccess, DomainError error)
        : base(isSuccess, error)
    {
        _value = value;
    }

    public static Result<T> Success(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new(value, true, DomainError.None);
    }

    public new static Result<T> Failure(DomainError error) => new(default, false, error);

    public static implicit operator Result<T>(T value) => Success(value);

    public static implicit operator Result<T>(DomainError error) => Failure(error);
}
