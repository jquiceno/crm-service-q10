namespace Shared.Application.Dtos;

public sealed record PageQueryInputDto(int PageIndex = 0, int PageSize = 20)
{
    /// <summary>
    /// Raised from 100 to the 5000 the legacy activities API allowed: the strangler has to answer
    /// the same page sizes its callers already use, and a lower cap would turn a request that
    /// works today into a 400.
    /// </summary>
    public const int MaxPageSize = 5000;
}
