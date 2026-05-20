namespace Shared.Domain.Pagination;

public sealed record PageQuery(int PageIndex, int PageSize)
{
    public int Skip => PageIndex * PageSize;
}
