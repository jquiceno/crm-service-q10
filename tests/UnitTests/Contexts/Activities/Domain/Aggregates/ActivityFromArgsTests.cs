using Activities.Domain;
using Activities.Domain.Aggregates;
using Activities.Domain.Enums;
using Activities.Domain.Errors;
using Shared.Results.Errors;
using Shouldly;
using Xunit;

namespace UnitTests.Contexts.Activities.Domain.Aggregates;

/// <summary>
/// Covers the args-based factories: the application-layer entry point that builds the value
/// objects inside the aggregate and accumulates their errors. The invariants themselves are
/// covered by <see cref="ActivityTests"/>.
/// </summary>
public sealed class ActivityFromArgsTests
{
    private static readonly DateTime Now = new(2026, 8, 21, 10, 30, 0, DateTimeKind.Unspecified);

    private static ScheduleActivityArgs ValidScheduleArgs => new(
        DealId: 1200,
        OpportunityId: 845,
        Type: ActivityType.Call,
        Description: "call the applicant",
        DueAt: Now.AddDays(1),
        AdvisorId: "339968541842",
        CreatedById: "339968541842");

    private static CompleteActivityArgs ValidCompleteArgs => new(
        DealId: 1200,
        OpportunityId: 845,
        Type: ActivityType.Call,
        Outcome: "the applicant answered",
        OutcomeName: nameof(CallOutcome.Contacted),
        DueAt: null,
        AdvisorId: "339968541842",
        CreatedById: "339968541842");

    [Fact]
    public void Schedule_FromArgs_BuildsTheValueObjectsItself()
    {
        var result = Activity.Schedule(ValidScheduleArgs, Now);

        result.IsSuccess.ShouldBeTrue();

        var activity = result.Value;
        activity.Status.ShouldBe(ActivityStatus.Scheduled);
        activity.Description!.Value.ShouldBe("call the applicant");
        activity.AdvisorId!.Value.ShouldBe("339968541842");
        activity.CreatedById.Value.ShouldBe("339968541842");
        activity.CreatedAt.ShouldBe(Now);
    }

    [Fact]
    public void Schedule_FromArgs_AccumulatesEveryValueObjectError()
    {
        var args = ValidScheduleArgs with
        {
            Description = null,
            AdvisorId = new string('9', ActivityLimits.PersonCodeMaxLength + 1),
            CreatedById = "",
        };

        var result = Activity.Schedule(args, Now);

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.DomainError);
        result.Error.Details.Select(d => d.Property).ShouldBe(
            [
                nameof(Activity.Description),
                nameof(Activity.AdvisorId),
                nameof(Activity.CreatedById),
            ],
            ignoreOrder: true);
    }

    [Fact]
    public void Schedule_FromArgs_ReportsThePropertyOfTheCreatedByIdSeparately()
    {
        var result = Activity.Schedule(ValidScheduleArgs with { CreatedById = null }, Now);

        result.IsFailure.ShouldBeTrue();

        var detail = result.Error.Details.ShouldHaveSingleItem();
        detail.Property.ShouldBe(nameof(Activity.CreatedById));
        detail.Errors!.ShouldBe(new[] { ActivityErrors.PersonCodeRequired.Message });
    }

    [Fact]
    public void Schedule_FromArgs_WrapsInvariantFailures()
    {
        var result = Activity.Schedule(ValidScheduleArgs with { DealId = 0 }, Now);

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.DomainError);

        var detail = result.Error.Details.ShouldHaveSingleItem();
        detail.Property.ShouldBe(nameof(Activity.DealId));
        detail.Errors!.ShouldBe(new[] { ActivityErrors.DealIdRequired.Message });
    }

    [Fact]
    public void RegisterCompleted_FromArgs_ResolvesTheOutcomeName()
    {
        var result = Activity.RegisterCompleted(ValidCompleteArgs, Now);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Status.ShouldBe(ActivityStatus.Completed);
        result.Value.OutcomeType!.Scope.ShouldBe(ActivityType.Call);
        result.Value.OutcomeType.Name.ShouldBe(nameof(CallOutcome.Contacted));
    }

    [Fact]
    public void RegisterCompleted_FromArgs_WithUnknownOutcomeName_AccumulatesTheError()
    {
        var result = Activity.RegisterCompleted(ValidCompleteArgs with { OutcomeName = "Nope" }, Now);

        result.IsFailure.ShouldBeTrue();

        var detail = result.Error.Details.ShouldHaveSingleItem();
        detail.Property.ShouldBe(nameof(Activity.OutcomeType));
        detail.Errors!.ShouldBe(new[] { ActivityErrors.UnknownOutcomeType.Message });
    }

    [Theory]
    [InlineData(ActivityType.Email)]
    [InlineData(ActivityType.Note)]
    [InlineData(ActivityType.WhatsApp)]
    public void RegisterCompleted_FromArgs_DiscardsTheOutcomeNameForTypesWithoutOutcome(
        ActivityType type)
    {
        var result = Activity.RegisterCompleted(ValidCompleteArgs with { Type = type }, Now);

        result.IsSuccess.ShouldBeTrue();
        result.Value.OutcomeType.ShouldBeNull();
    }

    [Fact]
    public void RegisterCompleted_FromArgs_WithMissingOutcomeName_ReportsOutcomeTypeRequired()
    {
        var result = Activity.RegisterCompleted(ValidCompleteArgs with { OutcomeName = null }, Now);

        result.IsFailure.ShouldBeTrue();

        var detail = result.Error.Details.ShouldHaveSingleItem();
        detail.Property.ShouldBe(nameof(Activity.OutcomeType));
        detail.Errors!.ShouldBe(new[] { ActivityErrors.OutcomeTypeRequired.Message });
    }
}
