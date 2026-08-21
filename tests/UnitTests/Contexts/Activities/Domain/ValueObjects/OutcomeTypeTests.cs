using Activities.Domain.Enums;
using Activities.Domain.Errors;
using Activities.Domain.ValueObjects;
using Shouldly;
using Xunit;

namespace UnitTests.Contexts.Activities.Domain.ValueObjects;

public sealed class OutcomeTypeTests
{
    [Fact]
    public void ForCall_WithKnownValue_ScopesToCall()
    {
        var result = OutcomeType.ForCall(CallOutcome.Contacted);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Scope.ShouldBe(ActivityType.Call);
        result.Value.Name.ShouldBe(nameof(CallOutcome.Contacted));
    }

    [Fact]
    public void ForMeeting_WithKnownValue_ScopesToMeeting()
    {
        var result = OutcomeType.ForMeeting(MeetingOutcome.Held);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Scope.ShouldBe(ActivityType.Meeting);
        result.Value.Name.ShouldBe(nameof(MeetingOutcome.Held));
    }

    [Fact]
    public void ForCall_WithUndefinedValue_ReturnsUnknownOutcomeType()
    {
        var result = OutcomeType.ForCall((CallOutcome)99);

        result.IsFailure.ShouldBeTrue();
        result.TypedError.ShouldBe(ActivityErrors.UnknownOutcomeType);
    }

    [Fact]
    public void Create_IsCaseInsensitive()
    {
        var result = OutcomeType.Create(ActivityType.Call, "contacted");

        result.IsSuccess.ShouldBeTrue();
        result.Value.Name.ShouldBe(nameof(CallOutcome.Contacted));
    }

    [Fact]
    public void Create_RejectsTheUnderlyingNumber()
    {
        // Enum.TryParse would happily turn "1" into a member; accepting it would leak a numeric
        // coupling the domain does not want.
        var result = OutcomeType.Create(ActivityType.Call, "1");

        result.IsFailure.ShouldBeTrue();
        result.TypedError.ShouldBe(ActivityErrors.UnknownOutcomeType);
    }

    [Fact]
    public void Create_RejectsAValueFromTheOtherCatalogue()
    {
        var result = OutcomeType.Create(ActivityType.Call, nameof(MeetingOutcome.Held));

        result.IsFailure.ShouldBeTrue();
        result.TypedError.ShouldBe(ActivityErrors.UnknownOutcomeType);
    }

    [Theory]
    [InlineData(ActivityType.Email)]
    [InlineData(ActivityType.Note)]
    [InlineData(ActivityType.WhatsApp)]
    [InlineData(ActivityType.VirtualMeeting)]
    public void Create_ForATypeWithoutOutcome_ReturnsScopeNotSupported(ActivityType type)
    {
        var result = OutcomeType.Create(type, nameof(CallOutcome.Contacted));

        result.IsFailure.ShouldBeTrue();
        result.TypedError.ShouldBe(ActivityErrors.OutcomeTypeScopeNotSupported);
    }

    [Fact]
    public void Create_WithNullName_ReturnsUnknownOutcomeType()
    {
        var result = OutcomeType.Create(ActivityType.Call, null);

        result.IsFailure.ShouldBeTrue();
        result.TypedError.ShouldBe(ActivityErrors.UnknownOutcomeType);
    }

    [Fact]
    public void IsDealClosed_IsTrueForBothCatalogues()
    {
        OutcomeType.ForCall(CallOutcome.DealClosed).Value.IsDealClosed.ShouldBeTrue();
        OutcomeType.ForMeeting(MeetingOutcome.DealClosed).Value.IsDealClosed.ShouldBeTrue();
    }

    [Fact]
    public void IsDealClosed_IsFalseForAnyOtherOutcome()
    {
        OutcomeType.ForCall(CallOutcome.Busy).Value.IsDealClosed.ShouldBeFalse();
        OutcomeType.ForMeeting(MeetingOutcome.Held).Value.IsDealClosed.ShouldBeFalse();
    }

    [Fact]
    public void Equality_ConsidersScopeAndName()
    {
        var call = OutcomeType.ForCall(CallOutcome.DealClosed).Value;
        var meeting = OutcomeType.ForMeeting(MeetingOutcome.DealClosed).Value;

        call.ShouldBe(OutcomeType.ForCall(CallOutcome.DealClosed).Value);
        call.ShouldNotBe(meeting);
    }
}
