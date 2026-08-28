using Activities.Application.Ports;
using Activities.Application.UseCases.CreateActivity;
using Activities.Domain.Aggregates;
using Activities.Domain.Enums;
using Activities.Domain.Errors;
using Activities.Domain.Models;
using Activities.Domain.Repositories;
using NSubstitute;
using Shared.Application.Ports;
using Shared.Results;
using Shared.Results.Errors;
using Shouldly;
using Xunit;

namespace UnitTests.Contexts.Activities.Application.UseCases;

/// <summary>
/// One test per conditional rule of the POST contract (§6.2), plus the write flow itself.
/// </summary>
public sealed class CreateActivityUseCaseTests
{
    private const string AdvisorIdentification = "1017123456";
    private const string AdvisorCode = "advisor-01";
    private const int DealId = 1200;
    private const int OpportunityId = 845;

    private static readonly DateTime Now = new(2026, 8, 28, 15, 0, 0, DateTimeKind.Utc);

    private readonly IActivityRepository _repository = Substitute.For<IActivityRepository>();
    private readonly IDealReader _dealReader = Substitute.For<IDealReader>();
    private readonly IAdvisorReader _advisorReader = Substitute.For<IAdvisorReader>();
    private readonly IUnitOfWorkPort _unitOfWork = Substitute.For<IUnitOfWorkPort>();
    private readonly TimeProvider _clock = Substitute.For<TimeProvider>();

    public CreateActivityUseCaseTests()
    {
        _advisorReader.ResolveByIdentificationAsync(AdvisorIdentification, Arg.Any<CancellationToken>())
            .Returns(AdvisorCode);
        _dealReader.GetDealContextAsync(DealId, Arg.Any<CancellationToken>())
            .Returns(new DealContext(Exists: true, OpportunityId, OpportunityArchived: false));
        _repository.AddAsync(Arg.Any<Activity>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());
        _unitOfWork.CommitAsync(Arg.Any<CancellationToken>()).Returns(Result.Success());
        _clock.GetUtcNow().Returns(new DateTimeOffset(Now));
    }

    private CreateActivityUseCase Sut =>
        new(_repository, _dealReader, _advisorReader, _unitOfWork, _clock);

    // --- Happy paths -------------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_SchedulesAnActivity_AndCommitsInItsOwnStep()
    {
        var result = await Sut.ExecuteAsync(Scheduled());

        result.IsSuccess.ShouldBeTrue();
        await _repository.Received(1).AddAsync(Arg.Any<Activity>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_DerivesTheOpportunityFromTheDeal_AndNeverFromTheRequest()
    {
        var persisted = await CapturePersistedAsync(Scheduled());

        persisted.OpportunityId.ShouldBe(OpportunityId);
        persisted.DealId.ShouldBe(DealId);
    }

    [Fact]
    public async Task ExecuteAsync_ResolvesTheAdvisorIdentificationIntoItsPersonCode()
    {
        var persisted = await CapturePersistedAsync(Scheduled());

        persisted.AdvisorId!.Value.ShouldBe(AdvisorCode);
        persisted.CreatedById.Value.ShouldBe(AdvisorCode);
    }

    [Fact]
    public async Task ExecuteAsync_CompletingACall_StampsTheCompletionWithTheServiceClock()
    {
        var persisted = await CapturePersistedAsync(Completed(outcomeType: "contacted"));

        persisted.Status.ShouldBe(ActivityStatus.Completed);
        persisted.CompletedAt.ShouldBe(Now);
        persisted.OutcomeType!.Name.ShouldBe(nameof(CallOutcome.Contacted));
    }

    [Theory]
    [InlineData("call", "contacted", nameof(CallOutcome.Contacted))]
    [InlineData("call", "wrong-number", nameof(CallOutcome.WrongNumber))]
    [InlineData("meeting", "held", nameof(MeetingOutcome.Held))]
    [InlineData("meeting", "cancelled", nameof(MeetingOutcome.Cancelled))]
    public async Task ExecuteAsync_ResolvesTheOutcomeAgainstTheCatalogueOfItsType(
        string type, string outcomeType, string expectedName)
    {
        var persisted = await CapturePersistedAsync(
            Completed(outcomeType) with { Type = type });

        persisted.OutcomeType!.Name.ShouldBe(expectedName);
    }

    [Fact]
    public async Task ExecuteAsync_AcceptsDealClosedAsAnOutcome()
    {
        var persisted = await CapturePersistedAsync(Completed(outcomeType: "deal-closed"));

        persisted.OutcomeType!.Name.ShouldBe(nameof(CallOutcome.DealClosed));
    }

    [Fact]
    public async Task ExecuteAsync_CompletingATypeWithoutACatalogue_DropsTheOutcomeTypeLikeTheLegacy()
    {
        var persisted = await CapturePersistedAsync(
            Completed(outcomeType: "contacted") with { Type = "whatsapp" });

        persisted.OutcomeType.ShouldBeNull();
    }

    // --- Status and type ---------------------------------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("done")]
    public async Task ExecuteAsync_WithAStatusThatIsNotAStatus_Fails(string? status)
    {
        var result = await Sut.ExecuteAsync(Scheduled() with { Status = status });

        await ShouldFailWithoutTouchingTheDatabase(result, ActivityErrors.InvalidActivityStatus.Message);
    }

    [Fact]
    public async Task ExecuteAsync_CreatingAnAlreadyCancelledActivity_FailsAsNotCreatable()
    {
        var result = await Sut.ExecuteAsync(Scheduled() with { Status = "cancelled" });

        await ShouldFailWithoutTouchingTheDatabase(result, ActivityErrors.StatusNotCreatable.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("phone-call")]
    public async Task ExecuteAsync_WithAnUnknownType_Fails(string? type)
    {
        var result = await Sut.ExecuteAsync(Scheduled() with { Type = type });

        await ShouldFailWithoutTouchingTheDatabase(result, ActivityErrors.InvalidActivityType.Message);
    }

    [Fact]
    public async Task ExecuteAsync_WithAVirtualMeeting_FailsAsNotWritable()
    {
        var result = await Sut.ExecuteAsync(Scheduled() with { Type = "virtual-meeting" });

        result.IsFailure.ShouldBeTrue();
        result.Error.Details.ShouldContain(detail =>
            detail.Errors!.Contains(ActivityErrors.TypeNotWritable.Message));
    }

    [Fact]
    public async Task ExecuteAsync_SchedulingANote_Fails()
    {
        var result = await Sut.ExecuteAsync(Scheduled() with { Type = "note" });

        result.IsFailure.ShouldBeTrue();
        result.Error.Details.ShouldContain(detail =>
            detail.Errors!.Contains(ActivityErrors.NoteCannotBeScheduled.Message));
    }

    // --- Fields the status forbids or requires -----------------------------------------------

    [Fact]
    public async Task ExecuteAsync_SchedulingWithAnOutcome_Fails()
    {
        var result = await Sut.ExecuteAsync(Scheduled() with { Outcome = "ya se hizo" });

        await ShouldFailWithoutTouchingTheDatabase(
            result, ActivityErrors.OutcomeNotAllowedWhenScheduled.Message);
    }

    [Fact]
    public async Task ExecuteAsync_SchedulingWithAnOutcomeType_Fails()
    {
        var result = await Sut.ExecuteAsync(Scheduled() with { OutcomeType = "contacted" });

        await ShouldFailWithoutTouchingTheDatabase(
            result, ActivityErrors.OutcomeTypeNotAllowedWhenScheduled.Message);
    }

    [Fact]
    public async Task ExecuteAsync_CompletingWithAPlannedDescription_Fails()
    {
        var result = await Sut.ExecuteAsync(
            Completed(outcomeType: "contacted") with { Description = "llamar mañana" });

        await ShouldFailWithoutTouchingTheDatabase(
            result, ActivityErrors.DescriptionNotAllowedWhenCompleted.Message);
    }

    [Fact]
    public async Task ExecuteAsync_SchedulingWithoutADescription_Fails()
    {
        var result = await Sut.ExecuteAsync(Scheduled() with { Description = null });

        result.IsFailure.ShouldBeTrue();
        result.Error.Details.ShouldContain(detail =>
            detail.Errors!.Contains(ActivityErrors.DescriptionRequired.Message));
    }

    [Fact]
    public async Task ExecuteAsync_SchedulingWithoutADueDate_Fails()
    {
        var result = await Sut.ExecuteAsync(Scheduled() with { DueAt = null });

        result.IsFailure.ShouldBeTrue();
        result.Error.Details.ShouldContain(detail =>
            detail.Errors!.Contains(ActivityErrors.DueDateRequired.Message));
    }

    [Fact]
    public async Task ExecuteAsync_CompletingWithoutAnOutcome_Fails()
    {
        var result = await Sut.ExecuteAsync(Completed(outcomeType: "contacted") with { Outcome = null });

        result.IsFailure.ShouldBeTrue();
        result.Error.Details.ShouldContain(detail =>
            detail.Errors!.Contains(ActivityErrors.OutcomeRequired.Message));
    }

    [Fact]
    public async Task ExecuteAsync_CompletingACallWithoutAnOutcomeType_Fails()
    {
        var result = await Sut.ExecuteAsync(Completed(outcomeType: null));

        result.IsFailure.ShouldBeTrue();
        result.Error.Details.ShouldContain(detail =>
            detail.Errors!.Contains(ActivityErrors.OutcomeTypeRequired.Message));
    }

    [Fact]
    public async Task ExecuteAsync_CompletingACallWithAnOutcomeOfAnotherCatalogue_Fails()
    {
        var result = await Sut.ExecuteAsync(Completed(outcomeType: "held"));

        result.IsFailure.ShouldBeTrue();
        result.Error.Details.ShouldContain(detail =>
            detail.Errors!.Contains(ActivityErrors.UnknownOutcomeType.Message));
    }

    // --- Lookups against the institution -----------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WithAnUnknownAdvisor_ReturnsNotFound()
    {
        _advisorReader.ResolveByIdentificationAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((string?)null);

        var result = await Sut.ExecuteAsync(Scheduled());

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.NotFound);
        result.Error.Message.ShouldContain(AdvisorIdentification);
        await _repository.DidNotReceive().AddAsync(Arg.Any<Activity>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WithAnUnknownDeal_ReturnsNotFound()
    {
        _dealReader.GetDealContextAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(DealContext.NotFound);

        var result = await Sut.ExecuteAsync(Scheduled());

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.NotFound);
        result.Error.Message.ShouldContain(DealId.ToString());
        await _repository.DidNotReceive().AddAsync(Arg.Any<Activity>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenTheOpportunityIsArchived_Fails()
    {
        _dealReader.GetDealContextAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new DealContext(Exists: true, OpportunityId, OpportunityArchived: true));

        var result = await Sut.ExecuteAsync(Scheduled());

        await ShouldFailWithoutTouchingTheDatabase(result, ActivityErrors.OpportunityArchived.Message);
    }

    [Fact]
    public async Task ExecuteAsync_WhatItCanJudgeFromTheRequestAlone_CostsNoQuery()
    {
        await Sut.ExecuteAsync(Scheduled() with { Type = "phone-call" });

        await _advisorReader.DidNotReceive()
            .ResolveByIdentificationAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _dealReader.DidNotReceive()
            .GetDealContextAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhatOnlyTheAggregateKnows_IsJudgedAfterTheLookups()
    {
        // The price of keeping the rule in one place: a type the service cannot write is only
        // rejected once the advisor and the deal have been resolved. Nothing is written.
        var result = await Sut.ExecuteAsync(Scheduled() with { Type = "virtual-meeting" });

        result.IsFailure.ShouldBeTrue();
        await _dealReader.Received(1).GetDealContextAsync(DealId, Arg.Any<CancellationToken>());
        await _repository.DidNotReceive().AddAsync(Arg.Any<Activity>(), Arg.Any<CancellationToken>());
    }

    // --- Persistence failures ----------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WhenAddFails_DoesNotCommit()
    {
        _repository.AddAsync(Arg.Any<Activity>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure(new DomainError("Persistence failure.", ErrorType.Internal)));

        var result = await Sut.ExecuteAsync(Scheduled());

        result.IsFailure.ShouldBeTrue();
        result.Error.Context.ShouldBe(ActivityErrors.Context);
        result.Error.Origin.ShouldBe(nameof(CreateActivityUseCase));
        await _unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// The commit is the only write, so its failure means the activity was not recorded — the
    /// caller can retry on it without duplicating anything.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WhenTheCommitFails_PropagatesTheError()
    {
        _unitOfWork.CommitAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Failure(new DomainError("Commit failure.", ErrorType.Internal)));

        var result = await Sut.ExecuteAsync(Scheduled());

        result.IsFailure.ShouldBeTrue();
        result.Error.Message.ShouldBe("Commit failure.");
        result.Error.Origin.ShouldBe(nameof(CreateActivityUseCase));
    }

    // --- Helpers -----------------------------------------------------------------------------

    private static CreateActivityInputDto Scheduled() =>
        new(
            DealId,
            Status: "scheduled",
            Type: "call",
            AdvisorIdentification: AdvisorIdentification,
            ActivityDate: Now,
            Description: "Llamar al cliente",
            Outcome: null,
            OutcomeType: null,
            DueAt: Now.AddDays(1));

    private static CreateActivityInputDto Completed(string? outcomeType) =>
        new(
            DealId,
            Status: "completed",
            Type: "call",
            AdvisorIdentification: AdvisorIdentification,
            ActivityDate: Now,
            Description: null,
            Outcome: "Se contactó al cliente",
            OutcomeType: outcomeType,
            DueAt: null);

    private async Task<Activity> CapturePersistedAsync(CreateActivityInputDto input)
    {
        var result = await Sut.ExecuteAsync(input).ConfigureAwait(true);
        result.IsSuccess.ShouldBeTrue();

        return (Activity)_repository.ReceivedCalls()
            .Single(call => call.GetMethodInfo().Name == nameof(IActivityRepository.AddAsync))
            .GetArguments()[0]!;
    }

    /// <summary>
    /// Asserts the failure carries the offending field in <c>Details</c> — the only part of the
    /// error the API serializes per field — and that no write was attempted.
    /// </summary>
    private async Task ShouldFailWithoutTouchingTheDatabase(Result result, string expectedMessage)
    {
        result.IsFailure.ShouldBeTrue();
        ShouldReport(result, expectedMessage);
        result.Error.Context.ShouldBe(ActivityErrors.Context);
        result.Error.Origin.ShouldBe(nameof(CreateActivityUseCase));
        await _repository.DidNotReceive()
            .AddAsync(Arg.Any<Activity>(), Arg.Any<CancellationToken>())
            .ConfigureAwait(true);
    }

    /// <summary>Every 400 of this endpoint reports the offending field the same way.</summary>
    private static void ShouldReport(Result result, string expectedMessage) =>
        result.Error.Details.ShouldContain(
            detail => detail.Errors!.Contains(expectedMessage),
            $"expected '{expectedMessage}' among the reported details");
}
