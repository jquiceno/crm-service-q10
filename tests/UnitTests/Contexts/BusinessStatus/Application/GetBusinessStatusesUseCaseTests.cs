using BusinessStatus.Application.UseCases.GetBusinessStatuses;
using BusinessStatus.Domain.Aggregates;
using BusinessStatus.Domain.Enums;
using BusinessStatus.Domain.Queries;
using BusinessStatus.Domain.Repositories;
using NSubstitute;
using Shared.Domain.Pagination;
using Shared.Results;
using Shared.Results.Errors;
using Shouldly;
using Xunit;

namespace UnitTests.Contexts.BusinessStatus.Application;

public sealed class GetBusinessStatusesUseCaseTests
{
    private readonly IBusinessStatusRepository _repository = Substitute.For<IBusinessStatusRepository>();

    private static readonly PageQuery FirstPage = new(pageIndex: 0, pageSize: 20);

    private GetBusinessStatusesUseCase Sut => new(_repository);

    private static BusinessStatusAggregate Aggregate(
        int id = 7,
        string name = "Negotiation",
        int? percentage = 50,
        string? color = "49ff7c",
        bool isActive = true) =>
        BusinessStatusAggregate.Reconstruct(id, name, percentage, color, isActive);

    private void Returns(params BusinessStatusAggregate[] aggregates) =>
        _repository
            .GetAsync(Arg.Any<BusinessStatusFilter>(), Arg.Any<PageQuery>(), Arg.Any<CancellationToken>())
            .Returns(PagedResult<BusinessStatusAggregate>.Success(aggregates, aggregates.Length));

    private async Task<BusinessStatusFilter> CapturedFilterAsync(GetBusinessStatusesInputDto input)
    {
        Returns();

        await Sut.ExecuteAsync(input, FirstPage).ConfigureAwait(false);

        return (BusinessStatusFilter)_repository.ReceivedCalls().Single().GetArguments()[0]!;
    }

    [Fact]
    public async Task ExecuteAsync_WithoutFilters_MapsEveryAggregateToItsOutputDto()
    {
        Returns(Aggregate(7, "Negotiation", 50, "49ff7c", isActive: true));

        var result = await Sut.ExecuteAsync(new GetBusinessStatusesInputDto(), FirstPage);

        result.IsSuccess.ShouldBeTrue();
        var item = result.Items.ShouldHaveSingleItem();
        item.Id.ShouldBe(7);
        item.Name.ShouldBe("Negotiation");
        item.Percentage.ShouldBe(50);
        item.Color.ShouldBe("49ff7c");
        item.IsActive.ShouldBeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_KeepsTheColourAndThePercentageRaw()
    {
        Returns(Aggregate(percentage: null, color: null));

        var result = await Sut.ExecuteAsync(new GetBusinessStatusesInputDto(), FirstPage);

        var item = result.Items.ShouldHaveSingleItem();
        item.Percentage.ShouldBeNull("a row without percentage is served as it is, never as 0");
        item.Color.ShouldBeNull("the legacy CCCCCC default is not resolved by this contract");
    }

    [Fact]
    public async Task ExecuteAsync_PropagatesTheTotalCountOfTheWholeQuery()
    {
        _repository
            .GetAsync(Arg.Any<BusinessStatusFilter>(), Arg.Any<PageQuery>(), Arg.Any<CancellationToken>())
            .Returns(PagedResult<BusinessStatusAggregate>.Success([Aggregate()], totalCount: 37));

        var result = await Sut.ExecuteAsync(new GetBusinessStatusesInputDto(), FirstPage);

        result.Items.Count.ShouldBe(1);
        result.TotalCount.ShouldBe(37);
    }

    [Fact]
    public async Task ExecuteAsync_TranslatesTheNameFilter()
    {
        var filter = await CapturedFilterAsync(new GetBusinessStatusesInputDto("Nego"));

        filter.Name.ShouldBe("Nego");
        filter.IsActive.ShouldBeNull();
        filter.Kind.ShouldBe(BusinessStatusKind.All);
    }

    [Fact]
    public async Task ExecuteAsync_TranslatesTheIntermediateKindFilter()
    {
        var filter = await CapturedFilterAsync(
            new GetBusinessStatusesInputDto(Kind: BusinessStatusKind.Intermediate));

        filter.Kind.ShouldBe(BusinessStatusKind.Intermediate);
    }

    [Fact]
    public async Task ExecuteAsync_WithoutKind_DefaultsToAll()
    {
        var filter = await CapturedFilterAsync(new GetBusinessStatusesInputDto(IsActive: false));

        filter.Kind.ShouldBe(BusinessStatusKind.All);
        filter.IsActive.ShouldBe(false);
    }

    [Fact]
    public async Task ExecuteAsync_ForwardsThePageUntouched()
    {
        Returns();
        var page = new PageQuery(pageIndex: 3, pageSize: 25);

        await Sut.ExecuteAsync(new GetBusinessStatusesInputDto(), page);

        await _repository.Received(1).GetAsync(
            Arg.Any<BusinessStatusFilter>(),
            Arg.Is<PageQuery>(p => p.PageIndex == 3 && p.PageSize == 25),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenTheRepositoryFails_PropagatesTheErrorUntouched()
    {
        var failure = new InternalError("A persistence error occurred.") { Origin = "BusinessStatusRepository" };
        _repository
            .GetAsync(Arg.Any<BusinessStatusFilter>(), Arg.Any<PageQuery>(), Arg.Any<CancellationToken>())
            .Returns(PagedResult<BusinessStatusAggregate>.Failure(failure));

        var result = await Sut.ExecuteAsync(new GetBusinessStatusesInputDto(), FirstPage);

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Internal);
        result.Error.Origin.ShouldBe(
            "BusinessStatusRepository",
            "the use case does not replace the origin of the failure");
    }
}
