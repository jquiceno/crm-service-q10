namespace Shared.Application.Dtos;

public sealed record PageQueryInputDto(int PageIndex = 0, int PageSize = 20)
{
    public const int MaxPageSize = 100;
}
