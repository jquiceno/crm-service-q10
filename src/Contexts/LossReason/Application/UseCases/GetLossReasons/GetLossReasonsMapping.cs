using LossReason.Domain.Aggregates;

namespace LossReason.Application.UseCases.GetLossReasons;

public static class GetLossReasonsMapping
{
    public static GetLossReasonsOutputDto ToOutputDto(this LossReasonAggregate aggregate) =>
        new(aggregate.Id, aggregate.Name, aggregate.IsActive);
}
