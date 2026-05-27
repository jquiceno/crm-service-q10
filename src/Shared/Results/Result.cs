using Shared.Results.Errors;

namespace Shared.Results;

public class Result
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;

    private readonly DomainError _error;

    public DomainError Error => IsFailure
        ? _error
        : throw new InvalidOperationException("Cannot access Error of a successful result.");

    protected Result(bool isSuccess, DomainError error)
    {
        if (isSuccess && !ReferenceEquals(error, DomainError.None))
            throw new InvalidOperationException("A successful result cannot have an error.");

        if (!isSuccess && ReferenceEquals(error, DomainError.None))
            throw new InvalidOperationException("A failed result must have an error.");

        IsSuccess = isSuccess;
        _error = error;
    }

    public static Result Success() => new(true, DomainError.None);
    public static Result Failure(DomainError error) => new(false, error);

    public static implicit operator Result(DomainError error) => Failure(error);
}

public class Result<T> : Result
{
    private readonly T? _value;

    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("Cannot access Value of a failed result.");

    protected Result(T? value, bool isSuccess, DomainError error)
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

public sealed class Result<TValue, TError> : Result<TValue> where TError : DomainError
{
    private readonly TError? _typedError;

    /// <summary>
    /// Typed error accessor. Use this instead of <see cref="Result.Error"/> when the concrete
    /// error type matters (e.g. to access <see cref="ValidationError.Property"/>).
    /// </summary>
    public TError TypedError => IsFailure
        ? _typedError!
        : throw new InvalidOperationException("Cannot access TypedError of a successful result.");

    private Result(TValue value) : base(value, true, DomainError.None)
    {
        _typedError = default;
    }

    private Result(TError error) : base(default, false, error)
    {
        _typedError = error;
    }

    public new static Result<TValue, TError> Success(TValue value) => new(value);
    public static Result<TValue, TError> Failure(TError error) => new(error);

    public static implicit operator Result<TValue, TError>(TValue value) => Success(value);
    public static implicit operator Result<TValue, TError>(TError error) => Failure(error);
}
