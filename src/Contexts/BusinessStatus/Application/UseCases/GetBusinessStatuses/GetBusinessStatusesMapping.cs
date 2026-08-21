using BusinessStatus.Domain.Aggregates;
using BusinessStatus.Domain.Enums;
using BusinessStatus.Domain.Queries;

namespace BusinessStatus.Application.UseCases.GetBusinessStatuses;

public static class GetBusinessStatusesMapping
{
    public static BusinessStatusFilter ToFilter(this GetBusinessStatusesInputDto input) =>
        new(input.Name, input.IsActive, input.Kind ?? BusinessStatusKind.All);

    public static GetBusinessStatusesOutputDto ToOutputDto(this BusinessStatusAggregate aggregate) =>
        new(aggregate.Id,
            aggregate.Name,
            aggregate.Percentage,
            aggregate.Color?.Value,
            aggregate.IsActive);
}
