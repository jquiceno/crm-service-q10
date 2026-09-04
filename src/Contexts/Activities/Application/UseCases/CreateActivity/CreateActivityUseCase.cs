using Activities.Application.Contracts;
using Activities.Application.Ports;
using Activities.Domain.Aggregates;
using Activities.Domain.Enums;
using Activities.Domain.Errors;
using Activities.Domain.Repositories;
using Shared.Results;
using Shared.Results.Errors;

namespace Activities.Application.UseCases.CreateActivity;

/// <summary>
/// Records an activity against a deal, either planned for the future or already completed.
/// </summary>
/// <remarks>
/// Order of the checks is deliberate: what can be judged from the request alone is judged first
/// (the status and type names, and the fields the status forbids), so those never cost a query.
/// The remaining invariants belong to the aggregate and are checked after the advisor and the
/// deal are resolved — a request that fails one of those does reach the institution's database
/// first, in exchange for keeping the rules in a single place.
/// <para>
/// A non-positive <c>dealId</c> is reported as a missing deal (404), not as a malformed field:
/// the shape of the request is the API edge's job (§6.2), and by the time the use case runs, an
/// id that matches no deal is indistinguishable from any other unknown id.
/// </para>
/// <para>
/// It writes nothing outside its own table: the opportunity's last-activity date and the audit
/// trail stay with their owners, and in phase 1 the monolith adapter keeps writing them
/// (DEC-4, DEC-11). The advisor's role is not checked here either — that is the caller's
/// responsibility (DEC-17).
/// </para>
/// <para>
/// The clock is <see cref="TimeProvider"/> for now, in UTC, and it only stamps <c>CompletedAt</c>:
/// <c>CreatedAt</c> is still stamped by <c>ActivityAggregate.Created()</c> with <c>DateTime.UtcNow</c>.
/// Both are the institution's clock once Tarea 4 lands <c>IClockPort</c> (DEC-12) — two places to
/// change, not one.
/// </para>
/// </remarks>
public sealed class CreateActivityUseCase(
    IActivityRepository repository,
    IDealReader dealReader,
    IAdvisorReader advisorReader,
    TimeProvider timeProvider) : ICreateActivityUseCase
{
    private const string Origin = nameof(CreateActivityUseCase);

    public async Task<Result<CreateActivityOutputDto>> ExecuteAsync(
        CreateActivityInputDto input, CancellationToken cancellationToken = default)
    {
        if (!ContractNames.TryParseStatus(input.Status, out var status))
            return Enrich(ActivityErrors.InvalidActivityStatus with { Value = input.Status });

        if (status == ActivityStatus.Cancelled)
            return Enrich(ActivityErrors.StatusNotCreatable with { Value = input.Status });

        if (!ContractNames.TryParseType(input.Type, out var type))
            return Enrich(ActivityErrors.InvalidActivityType with { Value = input.Type });

        var conflictingField = FindFieldNotAllowedFor(status, input);
        if (conflictingField is not null)
            return Enrich(conflictingField);

        var advisorCode = await advisorReader
            .ResolveByIdentificationAsync(input.AdvisorIdentification, cancellationToken)
            .ConfigureAwait(false);

        if (advisorCode is null)
            return Enrich(ActivityErrors.AdvisorNotFound(input.AdvisorIdentification));

        var createdByCode = advisorCode;
        if (!string.IsNullOrWhiteSpace(input.CreatedByIdentification))
        {
            createdByCode = await advisorReader
                .ResolveByIdentificationAsync(input.CreatedByIdentification, cancellationToken)
                .ConfigureAwait(false);

            if (createdByCode is null)
                return Enrich(ActivityErrors.CreatedByNotFound(input.CreatedByIdentification));
        }

        var deal = await dealReader
            .GetDealContextAsync(input.DealId, cancellationToken)
            .ConfigureAwait(false);

        if (!deal.Exists)
            return Enrich(ActivityErrors.DealNotFound(input.DealId));

        if (deal.OpportunityArchived)
            return Enrich(ActivityErrors.OpportunityArchived with { Value = input.DealId });

        var activityResult = status == ActivityStatus.Scheduled
            ? ActivityAggregate.Schedule(input.ToScheduleArgs(type, deal.OpportunityId, advisorCode, createdByCode))
            : ActivityAggregate.RegisterCompleted(
                input.ToCompleteArgs(type, deal.OpportunityId, advisorCode, createdByCode),
                timeProvider.GetUtcNow().UtcDateTime);

        if (activityResult.IsFailure)
            return Enrich(activityResult.Error);

        // CreateAsync confirms its own insert and hands the activity back carrying the generated
        // consecutive, so there is no unit of work to commit afterwards. Its failure travels
        // untouched: the repository already sealed where it happened.
        var createResult = await repository
            .CreateAsync(activityResult.Value, cancellationToken)
            .ConfigureAwait(false);

        if (createResult.IsFailure)
            return createResult.Error;

        return createResult.Value.ToOutputDto();
    }

    /// <summary>
    /// Reports a field the status forbids. The aggregate cannot: its arguments carry no outcome
    /// when scheduling and no description when completing, so without this check the service would
    /// silently drop what the caller sent — exactly the swallowing DEC-13 rules out.
    /// </summary>
    private static ValidationError? FindFieldNotAllowedFor(
        ActivityStatus status, CreateActivityInputDto input)
    {
        if (status == ActivityStatus.Scheduled)
        {
            if (!string.IsNullOrWhiteSpace(input.Outcome))
                return ActivityErrors.OutcomeNotAllowedWhenScheduled with { Value = input.Outcome };

            if (!string.IsNullOrWhiteSpace(input.OutcomeType))
                return ActivityErrors.OutcomeTypeNotAllowedWhenScheduled with { Value = input.OutcomeType };

            return null;
        }

        return string.IsNullOrWhiteSpace(input.Description)
            ? null
            : ActivityErrors.DescriptionNotAllowedWhenCompleted with { Value = input.Description };
    }

    /// <summary>
    /// Seals an error this use case originates — its own rules and the aggregate's — with the
    /// context and origin, and wraps a bare validation error into the same envelope the
    /// aggregate's failures use.
    /// </summary>
    /// <remarks>
    /// Only for errors born here. What the repository or a reader returns travels untouched: they
    /// already stamped where the failure happened, and rewriting it would make the log name this
    /// class for something that broke elsewhere.
    /// <para>
    /// The wrapping exists because the error the API serializes reads only
    /// <c>DomainError.Details</c>, which a lone <see cref="ValidationError"/> leaves empty — so its
    /// <c>Property</c> and <c>Value</c>, the very fields the monolith adapter needs to report the
    /// offending input, would never reach the caller.
    /// </para>
    /// </remarks>
    private static DomainError Enrich(DomainError error)
    {
        var enveloped = error is ValidationError validation
            ? DomainError.FromValidationDomainErrors([validation])
            : error;

        return enveloped with { Context = ActivityErrors.Context, Origin = Origin };
    }
}
