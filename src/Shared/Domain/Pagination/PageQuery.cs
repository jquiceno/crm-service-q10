namespace Shared.Domain.Pagination;

/// <summary>
/// Elemento de paginación (Extensible para futuras mejoras, como ordenamiento o filtrado)
/// </summary>
public sealed class PageQuery(int pageIndex, int pageSize)
{
    public int PageIndex { get; } = pageIndex;
    public int PageSize { get; } = pageSize;
    public int Skip => PageIndex * PageSize;
}