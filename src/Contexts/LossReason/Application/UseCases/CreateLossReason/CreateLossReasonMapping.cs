using LossReason.Domain.Aggregates;
using Shared.Results;

namespace LossReason.Application.UseCases.CreateLossReason;

public static class CreateLossReasonMapping
{
    public static Result<LossReasonAggregate> ToAggregate(this CreateLossReasonInputDto input) =>
        LossReasonAggregate.Create(new CreateLossReasonArgs(input.Name, input.IsActive));

    public static CreateLossReasonOutputDto ToOutputDto(this LossReasonAggregate aggregate) =>
        new(aggregate.Id, aggregate.Name, aggregate.IsActive);
}
