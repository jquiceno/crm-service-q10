using Shared.Domain.Errors;

namespace Shared.Domain.Result;

public sealed class PagedResult<T> : Result
{
    private readonly IReadOnlyList<T>? _items;
    private readonly int _totalCount;

    public IReadOnlyList<T> Items => IsSuccess
        ? _items!
        : throw new InvalidOperationException("Cannot access Items of a failed result.");

    public int TotalCount => IsSuccess
        ? _totalCount
        : throw new InvalidOperationException("Cannot access TotalCount of a failed result.");

    private PagedResult(IReadOnlyList<T> items, int totalCount) : base(true, DomainError.None)
    {
        _items = items;
        _totalCount = totalCount;
    }

    private PagedResult(DomainError error) : base(false, error)
    {
        _items = null;
        _totalCount = 0;
    }

    public static PagedResult<T> Success(IReadOnlyList<T> items, int totalCount) => new(items, totalCount);
    public new static PagedResult<T> Failure(DomainError error) => new(error);

    public static implicit operator PagedResult<T>(DomainError error) => Failure(error);
}
