using Activities.Application.Contracts;
using Activities.Domain.Enums;
using Shouldly;
using Xunit;

namespace UnitTests.Contexts.Activities.Application.Contracts;

/// <summary>The contract's names are part of the API surface: both directions are pinned here.</summary>
public sealed class ContractNamesTests
{
    [Theory]
    [InlineData(ActivityType.Call, "call")]
    [InlineData(ActivityType.WhatsApp, "whatsapp")]
    [InlineData(ActivityType.Email, "email")]
    [InlineData(ActivityType.Note, "note")]
    [InlineData(ActivityType.Meeting, "meeting")]
    [InlineData(ActivityType.VirtualMeeting, "virtual-meeting")]
    public void ToContract_RendersTheTypeName(ActivityType type, string expected) =>
        ContractNames.ToContract(type).ShouldBe(expected);

    [Fact]
    public void ToContract_RendersALegacyMeetingAsAPlainMeeting() =>
        ContractNames.ToContract(ActivityType.LegacyMeeting).ShouldBe("meeting");

    [Theory]
    [InlineData("call", ActivityType.Call)]
    [InlineData("whatsapp", ActivityType.WhatsApp)]
    [InlineData("MEETING", ActivityType.Meeting)]
    [InlineData("virtual-meeting", ActivityType.VirtualMeeting)]
    public void TryParseType_ResolvesTheContractName(string name, ActivityType expected)
    {
        ContractNames.TryParseType(name, out var type).ShouldBeTrue();
        type.ShouldBe(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("phone-call")]
    [InlineData("1")]
    public void TryParseType_RejectsWhatTheContractDoesNotDefine(string? name) =>
        ContractNames.TryParseType(name, out _).ShouldBeFalse();

    [Theory]
    [InlineData(ActivityStatus.Scheduled, "scheduled")]
    [InlineData(ActivityStatus.Completed, "completed")]
    [InlineData(ActivityStatus.Cancelled, "cancelled")]
    public void ToContract_RendersTheStatusName(ActivityStatus status, string expected) =>
        ContractNames.ToContract(status).ShouldBe(expected);

    [Theory]
    [InlineData("scheduled", ActivityStatus.Scheduled)]
    [InlineData("Completed", ActivityStatus.Completed)]
    public void TryParseStatus_ResolvesTheContractName(string name, ActivityStatus expected)
    {
        ContractNames.TryParseStatus(name, out var status).ShouldBeTrue();
        status.ShouldBe(expected);
    }

    [Fact]
    public void TryParseStatus_RejectsAnUnknownName() =>
        ContractNames.TryParseStatus("done", out _).ShouldBeFalse();

    [Theory]
    [InlineData("no-answer", "noanswer")]
    [InlineData("deal-closed", "dealclosed")]
    [InlineData("Contacted", "contacted")]
    public void ToOutcomeName_StripsTheDashesForTheDomainToResolve(string contractName, string expected) =>
        ContractNames.ToOutcomeName(contractName).ShouldBe(expected);

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public void ToOutcomeName_ReturnsNullWhenNothingWasSent(string? contractName) =>
        ContractNames.ToOutcomeName(contractName).ShouldBeNull();

    [Fact]
    public void ToContract_AndTryParseType_RoundTripEveryTypeButTheLegacyMeeting()
    {
        foreach (var type in Enum.GetValues<ActivityType>())
        {
            ContractNames.TryParseType(ContractNames.ToContract(type), out var parsed)
                .ShouldBeTrue($"'{ContractNames.ToContract(type)}' must parse back");

            // The only deliberate loss: the legacy '3' rows are reported as plain meetings, so
            // they come back as Meeting. Nothing writes either value anyway (DEC-5).
            parsed.ShouldBe(type == ActivityType.LegacyMeeting ? ActivityType.Meeting : type);
        }
    }

    [Fact]
    public void ToContract_AndTryParseStatus_RoundTripEveryStatus()
    {
        foreach (var status in Enum.GetValues<ActivityStatus>())
        {
            ContractNames.TryParseStatus(ContractNames.ToContract(status), out var parsed).ShouldBeTrue();
            parsed.ShouldBe(status);
        }
    }

    [Theory]
    [InlineData(nameof(CallOutcome.NoAnswer), "no-answer")]
    [InlineData(nameof(CallOutcome.WrongNumber), "wrong-number")]
    [InlineData(nameof(CallOutcome.DealClosed), "deal-closed")]
    [InlineData(nameof(MeetingOutcome.Held), "held")]
    public void ToOutcomeContract_RendersTheOutcomeName(string outcomeName, string expected) =>
        ContractNames.ToOutcomeContract(outcomeName).ShouldBe(expected);
}
