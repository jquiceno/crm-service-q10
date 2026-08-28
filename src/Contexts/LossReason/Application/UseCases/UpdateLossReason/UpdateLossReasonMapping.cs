using LossReason.Domain.Aggregates;

namespace LossReason.Application.UseCases.UpdateLossReason;

public static class UpdateLossReasonMapping
{
    public static UpdateLossReasonArgs ToUpdateArgs(this UpdateLossReasonInputDto input) =>
        new(input.Name, input.IsActive);

    public static UpdateLossReasonOutputDto ToOutputDto(this LossReasonAggregate aggregate) =>
        new(aggregate.Id, aggregate.Name, aggregate.IsActive);
}
