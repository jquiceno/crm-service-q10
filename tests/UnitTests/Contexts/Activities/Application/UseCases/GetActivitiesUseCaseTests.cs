using Activities.Application.UseCases.GetActivities;
using Activities.Domain.Aggregates;
using Activities.Domain.Enums;
using Activities.Domain.Queries;
using Activities.Domain.Models;
using Activities.Domain.Repositories;
using Activities.Domain.ValueObjects;
using NSubstitute;
using Shared.Domain.Pagination;
using Shared.Results;
using Shared.Results.Errors;
using Shouldly;
using Xunit;

namespace UnitTests.Contexts.Activities.Application.UseCases;

/// <summary>
/// The listing's own logic: build the filter, forward the page, map each row to the contract.
/// </summary>
public sealed class GetActivitiesUseCaseTests
{
    private readonly IActivityRepository _repository = Substitute.For<IActivityRepository>();

    private GetActivitiesUseCase Sut => new(_repository);

    [Fact]
    public async Task ExecuteAsync_BuildsTheFilterFromTheInput()
    {
        SearchReturns(PagedResult<ActivityListItem>.Success([], 0));

        await Sut.ExecuteAsync(new GetActivitiesInputDto(1200, 845, 3), new PageQuery(0, 30));

        await _repository.Received(1).SearchAsync(
            new ActivityFilter(1200, 845, 3), Arg.Any<PageQuery>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// The use case does not police the page: the size limits of §6.1 (1–5000, the legacy cap) are
    /// the request validator's, and <c>PageQueryInputDto.MaxPageSize</c> already carries them.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_ForwardsThePageUntouched()
    {
        var page = new PageQuery(2, 5000);
        SearchReturns(PagedResult<ActivityListItem>.Success([], 0));

        await Sut.ExecuteAsync(new GetActivitiesInputDto(1200, null, null), page);

        await _repository.Received(1).SearchAsync(
            Arg.Any<ActivityFilter>(), page, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_MapsACompletedCallToTheContractShape()
    {
        var activity = ActivityAggregate.RegisterCompleted(
            new CompleteActivityArgs(
                1200, 845, ActivityType.Call, "Se contactó al cliente", nameof(CallOutcome.Contacted),
                DueAt: null, "advisor-01", "advisor-01"),
            new DateTime(2026, 8, 1, 10, 20, 0, DateTimeKind.Utc)).Value;

        SearchReturns(PagedResult<ActivityListItem>.Success(
            [new ActivityListItem(activity, "Negocio", "Oportunidad", "Ana Pérez", "1017123456", "Carlos Ruiz")], 128));

        var result = await Sut.ExecuteAsync(new GetActivitiesInputDto(1200, null, null), new PageQuery(0, 30));

        result.IsSuccess.ShouldBeTrue();
        result.TotalCount.ShouldBe(128);

        var dto = result.Items.ShouldHaveSingleItem();
        dto.DealId.ShouldBe(1200);
        dto.DealName.ShouldBe("Negocio");
        dto.OpportunityId.ShouldBe(845);
        dto.OpportunityName.ShouldBe("Oportunidad");
        dto.Type.ShouldBe("call");
        dto.Status.ShouldBe("completed");
        dto.Description.ShouldBeNull();
        dto.Outcome.ShouldBe("Se contactó al cliente");
        dto.OutcomeType.ShouldBe("contacted");
        dto.AdvisorId.ShouldBe("advisor-01");
        dto.AdvisorName.ShouldBe("Ana Pérez");
        dto.AdvisorIdentification.ShouldBe("1017123456");
        dto.CompletedAt.ShouldBe(new DateTime(2026, 8, 1, 10, 20, 0, DateTimeKind.Utc));
        dto.DueAt.ShouldBeNull();
        dto.CreatedById.ShouldBe("advisor-01");
        dto.CreatedByName.ShouldBe("Carlos Ruiz");
    }

    [Fact]
    public async Task ExecuteAsync_MapsAScheduledActivityWithoutInventingAnOutcome()
    {
        var dueAt = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);
        var activity = ActivityAggregate.Schedule(
            1200, 845, ActivityType.Meeting, Description.Create("Visitar al cliente").Value, dueAt,
            PersonCode.Create("advisor-01").Value, PersonCode.Create("advisor-01").Value).Value;

        SearchReturns(PagedResult<ActivityListItem>.Success(
            [new ActivityListItem(activity, null, null, null, null, null)], 1));

        var dto = (await Sut.ExecuteAsync(new GetActivitiesInputDto(1200, null, null), new PageQuery(0, 30)))
            .Items.ShouldHaveSingleItem();

        dto.Status.ShouldBe("scheduled");
        dto.Type.ShouldBe("meeting");
        dto.Description.ShouldBe("Visitar al cliente");
        dto.DueAt.ShouldBe(dueAt);
        dto.Outcome.ShouldBeNull();
        dto.OutcomeType.ShouldBeNull();
        dto.CompletedAt.ShouldBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_WhenTheRepositoryFails_PropagatesTheErrorUntouched()
    {
        var error = new DomainError("Persistence failure.", ErrorType.Internal) { Origin = "Adapter" };
        SearchReturns(PagedResult<ActivityListItem>.Failure(error));

        var result = await Sut.ExecuteAsync(new GetActivitiesInputDto(1200, null, null), new PageQuery(0, 30));

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(error);
        result.Error.Origin.ShouldBe("Adapter");
    }

    private void SearchReturns(PagedResult<ActivityListItem> result) =>
        _repository
            .SearchAsync(Arg.Any<ActivityFilter>(), Arg.Any<PageQuery>(), Arg.Any<CancellationToken>())
            .Returns(result);
}
