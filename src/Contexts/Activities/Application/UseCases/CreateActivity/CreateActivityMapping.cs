using Activities.Application.Contracts;
using Activities.Domain.Aggregates;
using Activities.Domain.Enums;

namespace Activities.Application.UseCases.CreateActivity;

/// <summary>
/// Builds the aggregate's creation arguments from the request.
/// </summary>
/// <remarks>
/// The type, the advisor's person code, the creator's person code and the opportunity are
/// resolved by the use case before getting here — the first by name, the rest against the
/// institution's tables — so this mapping only assembles, it never validates.
/// <para>
/// <c>createdByCode</c> falls back to the advisor's code when the request carries no
/// <c>CreatedByIdentification</c> — resolved by the use case, not here: this class only accepts
/// the code it is handed and assembles the args.
/// </para>
/// </remarks>
public static class CreateActivityMapping
{
    public static ScheduleActivityArgs ToScheduleArgs(
        this CreateActivityInputDto input, ActivityType type, int? opportunityId, string advisorCode, string createdByCode) =>
        new(input.DealId, opportunityId, type, input.Description, input.DueAt, advisorCode, createdByCode);

    public static CompleteActivityArgs ToCompleteArgs(
        this CreateActivityInputDto input, ActivityType type, int? opportunityId, string advisorCode, string createdByCode) =>
        new(
            input.DealId,
            opportunityId,
            type,
            input.Outcome,
            ContractNames.ToOutcomeName(input.OutcomeType),
            input.DueAt,
            advisorCode,
            createdByCode);

    public static CreateActivityOutputDto ToOutputDto(this ActivityAggregate aggregate) => new(aggregate.Id);
}
