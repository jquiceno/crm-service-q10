using LossReason.Domain.Aggregates;

namespace LossReason.Application.UseCases.GetLossReasonById;

public static class GetLossReasonByIdMapping
{
    public static GetLossReasonByIdOutputDto ToOutputDto(this LossReasonAggregate aggregate) =>
        new(aggregate.Id, aggregate.Name, aggregate.IsActive);
}
