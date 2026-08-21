using LossReason.Domain.Aggregates;
using LossReason.Domain.Queries;

namespace LossReason.Application.UseCases.GetLossReasons;

public static class GetLossReasonsMapping
{
    public static LossReasonFilter ToFilter(this GetLossReasonsInputDto input) =>
        new(input.Name, input.IsActive);

    public static GetLossReasonsOutputDto ToOutputDto(this LossReasonAggregate aggregate) =>
        new(aggregate.Id, aggregate.Name, aggregate.IsActive);
}
