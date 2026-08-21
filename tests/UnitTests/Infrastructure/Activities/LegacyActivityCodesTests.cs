using Activities.Domain.Enums;
using Activities.Domain.ValueObjects;
using Infrastructure.Persistence.EntityFramework.Activities;
using Shouldly;
using Xunit;

namespace UnitTests.Infrastructure.Activities;

public sealed class LegacyActivityCodesTests
{
    [Theory]
    [InlineData(ActivityType.Call, "1")]
    [InlineData(ActivityType.Email, "2")]
    [InlineData(ActivityType.LegacyMeeting, "3")]
    [InlineData(ActivityType.Note, "4")]
    [InlineData(ActivityType.Meeting, "5")]
    [InlineData(ActivityType.VirtualMeeting, "6")]
    [InlineData(ActivityType.WhatsApp, "7")]
    public void TypeCodes_RoundTripEveryMember(ActivityType type, string code)
    {
        LegacyActivityCodes.ToTypeCode(type).ShouldBe(code);
        LegacyActivityCodes.ToType(code).ShouldBe(type);
    }

    [Theory]
    [InlineData(CallOutcome.NoAnswer, "1")]
    [InlineData(CallOutcome.Busy, "2")]
    [InlineData(CallOutcome.WrongNumber, "3")]
    [InlineData(CallOutcome.Voicemail, "5")]
    [InlineData(CallOutcome.Contacted, "6")]
    [InlineData(CallOutcome.DealClosed, "7")]
    public void CallOutcomeCodes_RoundTripEveryMember_SkippingTheLegacyHole(
        CallOutcome outcome, string code)
    {
        var outcomeType = OutcomeType.ForCall(outcome).Value;

        LegacyActivityCodes.ToOutcomeTypeCode(outcomeType).ShouldBe(code);
        LegacyActivityCodes.ToOutcomeType(ActivityType.Call, code).ShouldBe(outcomeType);
    }

    [Theory]
    [InlineData(MeetingOutcome.Held, "1")]
    [InlineData(MeetingOutcome.Cancelled, "2")]
    [InlineData(MeetingOutcome.DealClosed, "3")]
    public void MeetingOutcomeCodes_RoundTripEveryMember(MeetingOutcome outcome, string code)
    {
        var outcomeType = OutcomeType.ForMeeting(outcome).Value;

        LegacyActivityCodes.ToOutcomeTypeCode(outcomeType).ShouldBe(code);
        LegacyActivityCodes.ToOutcomeType(ActivityType.Meeting, code).ShouldBe(outcomeType);
    }

    [Fact]
    public void SameChar_MeansDifferentOutcomes_PerActivityType()
    {
        // '3' is the poster child of the scope-dependent column (§4 trampa).
        LegacyActivityCodes.ToOutcomeType(ActivityType.Call, "3")!
            .Name.ShouldBe(nameof(CallOutcome.WrongNumber));
        LegacyActivityCodes.ToOutcomeType(ActivityType.Meeting, "3")!
            .Name.ShouldBe(nameof(MeetingOutcome.DealClosed));
    }

    [Theory]
    [InlineData(ActivityType.LegacyMeeting)]
    [InlineData(ActivityType.VirtualMeeting)]
    public void EveryMeetingFlavour_ResolvesWithTheMeetingCatalogue(ActivityType type)
    {
        // The monolith reads the column with EstadoLlamada only for '1' and EstadoReunion for
        // every other meeting flavour, including the virtual one.
        var outcomeType = LegacyActivityCodes.ToOutcomeType(type, "1");

        outcomeType!.Name.ShouldBe(nameof(MeetingOutcome.Held));
    }

    [Theory]
    [InlineData(ActivityType.Email)]
    [InlineData(ActivityType.Note)]
    [InlineData(ActivityType.WhatsApp)]
    public void StrayOutcomeCodes_OnTypesWithoutACatalogue_AreDiscardedOnRead(ActivityType type)
    {
        LegacyActivityCodes.ToOutcomeType(type, "1").ShouldBeNull();
        LegacyActivityCodes.OwnsOutcomeCode(type).ShouldBeFalse(
            "the save side must never overwrite a column reads discard");
    }

    [Fact]
    public void NullCode_ReadsAsNoOutcomeType()
    {
        LegacyActivityCodes.ToOutcomeType(ActivityType.Call, null).ShouldBeNull();
        LegacyActivityCodes.ToOutcomeTypeCode(null).ShouldBeNull();
    }

    [Fact]
    public void UnknownTypeChar_FailsExplicitly_NamingTheColumn()
    {
        var exception = Should.Throw<InvalidOperationException>(() => LegacyActivityCodes.ToType("9"));

        exception.ShouldNotBeOfType<KeyNotFoundException>("D20: never a KeyNotFoundException");
        exception.Message.ShouldContain("negact_tipo");
        exception.Message.ShouldContain("'9'");
    }

    [Fact]
    public void TheCallCatalogueHole_FailsExplicitly_NamingTheColumn()
    {
        // '4' was commented out of the legacy call catalogue years ago and must never resolve.
        var exception = Should.Throw<InvalidOperationException>(
            () => LegacyActivityCodes.ToOutcomeType(ActivityType.Call, "4"));

        exception.ShouldNotBeOfType<KeyNotFoundException>("D20: never a KeyNotFoundException");
        exception.Message.ShouldContain("negact_resultado");
        exception.Message.ShouldContain("'4'");
    }
}
