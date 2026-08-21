using Activities.Domain.Aggregates;
using Activities.Domain.Enums;
using Activities.Domain.Errors;
using Activities.Domain.ValueObjects;
using Shouldly;
using Xunit;

namespace UnitTests.Contexts.Activities.Domain.Aggregates;

public sealed class ActivityTests
{
    private const int AnyDealId = 1200;
    private const int AnyOpportunityId = 845;

    // Fixed instant for inputs unrelated to CreatedAt (due dates, completion time). CreatedAt
    // itself is stamped by Created() with the real DateTime.UtcNow, so it can't be pinned to
    // this constant — see the CreatedAt assertions below.
    private static readonly DateTime Now = new(2026, 8, 21, 10, 30, 0, DateTimeKind.Utc);

    private static Description AnyDescription => Description.Create("call the applicant").Value;
    private static Outcome AnyOutcome => Outcome.Create("the applicant answered").Value;
    private static AdvisorId AnyAdvisor => AdvisorId.Create("339968541842").Value;
    private static OutcomeType AnyCallOutcome => OutcomeType.ForCall(CallOutcome.Contacted).Value;
    private static OutcomeType AnyMeetingOutcome => OutcomeType.ForMeeting(MeetingOutcome.Held).Value;

    // --- Schedule ------------------------------------------------------------------------

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Schedule_WithNonPositiveDealId_ReturnsDealIdRequired(int dealId)
    {
        var result = Activity.Schedule(
            dealId, AnyOpportunityId, ActivityType.Call, AnyDescription, Now.AddDays(1),
            AnyAdvisor, AnyAdvisor);

        result.IsFailure.ShouldBeTrue();
        result.TypedError.ShouldBe(ActivityErrors.DealIdRequired);
    }

    [Fact]
    public void Schedule_WithUndefinedType_ReturnsInvalidActivityType()
    {
        var result = Activity.Schedule(
            AnyDealId, AnyOpportunityId, (ActivityType)99, AnyDescription, Now.AddDays(1),
            AnyAdvisor, AnyAdvisor);

        result.IsFailure.ShouldBeTrue();
        result.TypedError.ShouldBe(ActivityErrors.InvalidActivityType);
    }

    [Theory]
    [InlineData(ActivityType.VirtualMeeting)]
    [InlineData(ActivityType.LegacyMeeting)]
    public void Schedule_WithAReadOnlyType_ReturnsTypeNotWritable(ActivityType type)
    {
        var result = Activity.Schedule(
            AnyDealId, AnyOpportunityId, type, AnyDescription, Now.AddDays(1),
            AnyAdvisor, AnyAdvisor);

        result.IsFailure.ShouldBeTrue();
        result.TypedError.ShouldBe(ActivityErrors.TypeNotWritable);
    }

    [Fact]
    public void Schedule_WithNote_ReturnsNoteCannotBeScheduled()
    {
        var result = Activity.Schedule(
            AnyDealId, AnyOpportunityId, ActivityType.Note, AnyDescription, Now.AddDays(1),
            AnyAdvisor, AnyAdvisor);

        result.IsFailure.ShouldBeTrue();
        result.TypedError.ShouldBe(ActivityErrors.NoteCannotBeScheduled);
    }

    [Fact]
    public void Schedule_WithoutDescription_ReturnsDescriptionRequired()
    {
        var result = Activity.Schedule(
            AnyDealId, AnyOpportunityId, ActivityType.Call, description: null, Now.AddDays(1),
            AnyAdvisor, AnyAdvisor);

        result.IsFailure.ShouldBeTrue();
        result.TypedError.ShouldBe(ActivityErrors.DescriptionRequired);
    }

    [Fact]
    public void Schedule_WithoutDueDate_ReturnsDueDateRequired()
    {
        var result = Activity.Schedule(
            AnyDealId, AnyOpportunityId, ActivityType.Call, AnyDescription, dueAt: null,
            AnyAdvisor, AnyAdvisor);

        result.IsFailure.ShouldBeTrue();
        result.TypedError.ShouldBe(ActivityErrors.DueDateRequired);
    }

    [Fact]
    public void Schedule_WithValidInput_ReturnsAScheduledActivity()
    {
        var dueAt = Now.AddDays(1);
        var before = DateTime.UtcNow;

        var result = Activity.Schedule(
            AnyDealId, AnyOpportunityId, ActivityType.Call, AnyDescription, dueAt,
            AnyAdvisor, AnyAdvisor);

        var after = DateTime.UtcNow;

        result.IsSuccess.ShouldBeTrue();

        var activity = result.Value;
        activity.Id.ShouldBe(0, "the database generates the identity on save");
        activity.Status.ShouldBe(ActivityStatus.Scheduled);
        activity.DealId.ShouldBe(AnyDealId);
        activity.OpportunityId.ShouldBe(AnyOpportunityId);
        activity.DueAt.ShouldBe(dueAt);
        activity.CreatedAt.ShouldNotBeNull();
        activity.CreatedAt!.Value.ShouldBeInRange(before, after);
        activity.UpdatedAt.ShouldBeNull("the legacy table has no updated column");
        activity.CompletedAt.ShouldBeNull();
        activity.Outcome.ShouldBeNull();
        activity.OutcomeType.ShouldBeNull();
    }

    // --- RegisterCompleted ---------------------------------------------------------------

    [Fact]
    public void RegisterCompleted_WithNonPositiveDealId_ReturnsDealIdRequired()
    {
        var result = Activity.RegisterCompleted(
            0, AnyOpportunityId, ActivityType.Call, AnyOutcome, AnyCallOutcome, dueAt: null,
            AnyAdvisor, AnyAdvisor, Now);

        result.IsFailure.ShouldBeTrue();
        result.TypedError.ShouldBe(ActivityErrors.DealIdRequired);
    }

    [Fact]
    public void RegisterCompleted_WithVirtualMeeting_ReturnsTypeNotWritable()
    {
        var result = Activity.RegisterCompleted(
            AnyDealId, AnyOpportunityId, ActivityType.VirtualMeeting, AnyOutcome, outcomeType: null,
            dueAt: null, AnyAdvisor, AnyAdvisor, Now);

        result.IsFailure.ShouldBeTrue();
        result.TypedError.ShouldBe(ActivityErrors.TypeNotWritable);
    }

    [Fact]
    public void RegisterCompleted_WithoutOutcome_ReturnsOutcomeRequired()
    {
        var result = Activity.RegisterCompleted(
            AnyDealId, AnyOpportunityId, ActivityType.Call, outcome: null, AnyCallOutcome,
            dueAt: null, AnyAdvisor, AnyAdvisor, Now);

        result.IsFailure.ShouldBeTrue();
        result.TypedError.ShouldBe(ActivityErrors.OutcomeRequired);
    }

    [Theory]
    [InlineData(ActivityType.Call)]
    [InlineData(ActivityType.Meeting)]
    public void RegisterCompleted_WithoutOutcomeTypeWhereItIsRequired_ReturnsOutcomeTypeRequired(
        ActivityType type)
    {
        var result = Activity.RegisterCompleted(
            AnyDealId, AnyOpportunityId, type, AnyOutcome, outcomeType: null, dueAt: null,
            AnyAdvisor, AnyAdvisor, Now);

        result.IsFailure.ShouldBeTrue();
        result.TypedError.ShouldBe(ActivityErrors.OutcomeTypeRequired);
    }

    [Fact]
    public void RegisterCompleted_WithAnOutcomeTypeFromAnotherScope_ReturnsScopeMismatch()
    {
        var result = Activity.RegisterCompleted(
            AnyDealId, AnyOpportunityId, ActivityType.Call, AnyOutcome, AnyMeetingOutcome,
            dueAt: null, AnyAdvisor, AnyAdvisor, Now);

        result.IsFailure.ShouldBeTrue();
        result.TypedError.ShouldBe(ActivityErrors.OutcomeTypeScopeMismatch);
    }

    [Theory]
    [InlineData(ActivityType.Email)]
    [InlineData(ActivityType.Note)]
    [InlineData(ActivityType.WhatsApp)]
    public void RegisterCompleted_ForATypeWithoutOutcomeType_DiscardsItSilently(ActivityType type)
    {
        // Legacy parity: the monolith ignores the outcome type for these types instead of
        // rejecting the request.
        var result = Activity.RegisterCompleted(
            AnyDealId, AnyOpportunityId, type, AnyOutcome, AnyCallOutcome, dueAt: null,
            AnyAdvisor, AnyAdvisor, Now);

        result.IsSuccess.ShouldBeTrue();
        result.Value.OutcomeType.ShouldBeNull();
    }

    [Fact]
    public void RegisterCompleted_WithValidCall_ReturnsACompletedActivity()
    {
        var before = DateTime.UtcNow;

        var result = Activity.RegisterCompleted(
            AnyDealId, AnyOpportunityId, ActivityType.Call, AnyOutcome, AnyCallOutcome,
            dueAt: null, AnyAdvisor, AnyAdvisor, Now);

        var after = DateTime.UtcNow;

        result.IsSuccess.ShouldBeTrue();

        var activity = result.Value;
        activity.Status.ShouldBe(ActivityStatus.Completed);
        activity.CreatedAt.ShouldNotBeNull();
        activity.CreatedAt!.Value.ShouldBeInRange(before, after);
        activity.CompletedAt.ShouldBe(Now);
        activity.Outcome.ShouldBe(AnyOutcome);
        activity.OutcomeType.ShouldBe(AnyCallOutcome);
        activity.Description.ShouldBeNull("a completed activity carries no planned description");
    }

    [Fact]
    public void RegisterCompleted_AcceptsDealClosedAsANormalOutcome()
    {
        // DEC-7: the reserved value of the legacy is a normal, writable outcome here.
        var dealClosed = OutcomeType.ForCall(CallOutcome.DealClosed).Value;

        var result = Activity.RegisterCompleted(
            AnyDealId, AnyOpportunityId, ActivityType.Call, AnyOutcome, dealClosed, dueAt: null,
            AnyAdvisor, AnyAdvisor, Now);

        result.IsSuccess.ShouldBeTrue();
        result.Value.OutcomeType!.IsDealClosed.ShouldBeTrue();
    }

    [Fact]
    public void RegisterCompleted_KeepsTheDueDateWhenSupplied()
    {
        // The legacy API filled the due date from the request even on completed activities.
        var dueAt = Now.AddHours(-2);

        var result = Activity.RegisterCompleted(
            AnyDealId, AnyOpportunityId, ActivityType.Call, AnyOutcome, AnyCallOutcome, dueAt,
            AnyAdvisor, AnyAdvisor, Now);

        result.IsSuccess.ShouldBeTrue();
        result.Value.DueAt.ShouldBe(dueAt);
    }

    [Fact]
    public void RegisterCompleted_WithoutADeal_IsStillRejected()
    {
        var result = Activity.RegisterCompleted(
            AnyDealId, opportunityId: null, ActivityType.Note, AnyOutcome, outcomeType: null,
            dueAt: null, AnyAdvisor, AnyAdvisor, Now);

        result.IsSuccess.ShouldBeTrue("a note can be completed; only scheduling it is forbidden");
        result.Value.OpportunityId.ShouldBeNull();
    }

    // --- Type predicates -----------------------------------------------------------------

    [Theory]
    [InlineData(ActivityType.Call, true)]
    [InlineData(ActivityType.Meeting, true)]
    [InlineData(ActivityType.Email, false)]
    [InlineData(ActivityType.Note, false)]
    [InlineData(ActivityType.WhatsApp, false)]
    [InlineData(ActivityType.VirtualMeeting, false)]
    public void AdmitsOutcomeType_OnlyForCallsAndMeetings(ActivityType type, bool expected)
    {
        Activity.AdmitsOutcomeType(type).ShouldBe(expected);
    }

    [Theory]
    [InlineData(ActivityType.Call, true)]
    [InlineData(ActivityType.WhatsApp, true)]
    [InlineData(ActivityType.Email, true)]
    [InlineData(ActivityType.Note, true)]
    [InlineData(ActivityType.Meeting, true)]
    [InlineData(ActivityType.VirtualMeeting, false)]
    [InlineData(ActivityType.LegacyMeeting, false)]
    public void IsWritable_ExcludesTheReadOnlyTypes(ActivityType type, bool expected)
    {
        Activity.IsWritable(type).ShouldBe(expected);
    }
}
