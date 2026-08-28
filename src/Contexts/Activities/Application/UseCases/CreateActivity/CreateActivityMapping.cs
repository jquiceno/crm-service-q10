using Activities.Application.Mapping;
using Activities.Domain.Aggregates;
using Activities.Domain.Enums;

namespace Activities.Application.UseCases.CreateActivity;

/// <summary>
/// Builds the aggregate's creation arguments from the request.
/// </summary>
/// <remarks>
/// The type, the advisor's person code and the opportunity are resolved by the use case before
/// getting here — the first by name, the other two against the institution's tables — so this
/// mapping only assembles, it never validates.
/// <para>
/// The advisor's code fills <c>CreatedById</c> as well. The legacy contract carries no separate
/// "who is registering" field and the endpoints are not authenticated yet (Tarea 10): until the
/// caller's identity travels with the request, the advisor is the only person the service knows,
/// and the legacy adapter behaves the same way.
/// </para>
/// </remarks>
public static class CreateActivityMapping
{
    public static ScheduleActivityArgs ToScheduleArgs(
        this CreateActivityInputDto input, ActivityType type, int? opportunityId, string advisorCode) =>
        new(input.DealId, opportunityId, type, input.Description, input.DueAt, advisorCode, advisorCode);

    public static CompleteActivityArgs ToCompleteArgs(
        this CreateActivityInputDto input, ActivityType type, int? opportunityId, string advisorCode) =>
        new(
            input.DealId,
            opportunityId,
            type,
            input.Outcome,
            ContractNames.ToOutcomeName(input.OutcomeType),
            input.DueAt,
            advisorCode,
            advisorCode);

    public static CreateActivityOutputDto ToOutputDto(this Activity aggregate) => new(aggregate.Id);
}
