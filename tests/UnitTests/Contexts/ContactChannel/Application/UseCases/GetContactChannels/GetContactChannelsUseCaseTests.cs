using ContactChannel.Application.UseCases.GetContactChannels;
using ContactChannel.Domain.Aggregates;
using ContactChannel.Domain.Queries;
using ContactChannel.Domain.Repositories;
using NSubstitute;
using Shared.Domain.Pagination;
using Shared.Results;
using Shared.Results.Errors;
using Shouldly;
using Xunit;

namespace UnitTests.Contexts.ContactChannel.Application.UseCases.GetContactChannels;

public sealed class GetContactChannelsUseCaseTests
{
    private readonly IContactChannelRepository _repository = Substitute.For<IContactChannelRepository>();

    private GetContactChannelsUseCase CreateUseCase() => new(_repository);

    private void ReturnsPage(params ContactChannelAggregate[] channels) =>
        _repository
            .GetAsync(Arg.Any<ContactChannelFilter>(), Arg.Any<PageQuery>(), Arg.Any<CancellationToken>())
            .Returns(PagedResult<ContactChannelAggregate>.Success(channels, channels.Length));

    [Fact]
    public async Task ExecuteAsync_MapsEveryChannelToTheOutputDto()
    {
        ReturnsPage(
            ContactChannelAggregate.Reconstruct(id: 1, name: "WhatsApp", isActive: true),
            ContactChannelAggregate.Reconstruct(id: 2, name: "Feria", isActive: false));

        var result = await CreateUseCase()
            .ExecuteAsync(new GetContactChannelsInputDto(IsActive: null, SearchName: null), new PageQuery(0, 10));

        result.IsSuccess.ShouldBeTrue();
        result.TotalCount.ShouldBe(2);
        result.Items.Select(c => c.Id).ShouldBe([1, 2]);
        result.Items.Select(c => c.Name).ShouldBe(["WhatsApp", "Feria"]);
        result.Items.Select(c => c.IsActive).ShouldBe([true, false]);
    }

    [Fact]
    public async Task ExecuteAsync_WithoutFilters_QueriesWithoutFilteringByStateOrName()
    {
        ReturnsPage();

        await CreateUseCase()
            .ExecuteAsync(new GetContactChannelsInputDto(IsActive: null, SearchName: null), new PageQuery(0, 10));

        await _repository.Received(1).GetAsync(
            Arg.Is<ContactChannelFilter>(f => f.IsActive == null && f.SearchName == null),
            Arg.Any<PageQuery>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_TranslatesTheInputIntoTheDomainFilter()
    {
        ReturnsPage();

        await CreateUseCase()
            .ExecuteAsync(new GetContactChannelsInputDto(IsActive: true, SearchName: "wha"), new PageQuery(0, 10));

        await _repository.Received(1).GetAsync(
            Arg.Is<ContactChannelFilter>(f => f.IsActive == true && f.SearchName == "wha"),
            Arg.Any<PageQuery>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_ForwardsThePageAndTheCancellationToken()
    {
        ReturnsPage();
        var page = new PageQuery(2, 25);
        using var cancellation = new CancellationTokenSource();

        await CreateUseCase()
            .ExecuteAsync(
                new GetContactChannelsInputDto(IsActive: null, SearchName: null),
                page,
                cancellation.Token);

        await _repository.Received(1).GetAsync(
            Arg.Any<ContactChannelFilter>(),
            page,
            cancellation.Token);
    }

    [Fact]
    public async Task ExecuteAsync_WithoutMatches_SucceedsWithAnEmptyPageInsteadOfFailing()
    {
        _repository
            .GetAsync(Arg.Any<ContactChannelFilter>(), Arg.Any<PageQuery>(), Arg.Any<CancellationToken>())
            .Returns(PagedResult<ContactChannelAggregate>.Success([], 0));

        var result = await CreateUseCase()
            .ExecuteAsync(new GetContactChannelsInputDto(IsActive: null, SearchName: null), new PageQuery(0, 10));

        result.IsSuccess.ShouldBeTrue();
        result.Items.ShouldBeEmpty();
        result.TotalCount.ShouldBe(0);
    }

    [Fact]
    public async Task ExecuteAsync_ReportsTheTotalOfTheWholeResultNotOfThePage()
    {
        _repository
            .GetAsync(Arg.Any<ContactChannelFilter>(), Arg.Any<PageQuery>(), Arg.Any<CancellationToken>())
            .Returns(PagedResult<ContactChannelAggregate>.Success(
                [ContactChannelAggregate.Reconstruct(id: 1, name: "WhatsApp", isActive: true)],
                42));

        var result = await CreateUseCase()
            .ExecuteAsync(new GetContactChannelsInputDto(IsActive: null, SearchName: null), new PageQuery(0, 1));

        result.Items.Count.ShouldBe(1);
        result.TotalCount.ShouldBe(42);
    }

    [Fact]
    public async Task ExecuteAsync_WhenTheRepositoryFails_PropagatesTheErrorWithoutReplacingItsOrigin()
    {
        var repositoryError = new InternalError("A persistence error occurred.") { Origin = "ContactChannelRepository" };
        _repository
            .GetAsync(Arg.Any<ContactChannelFilter>(), Arg.Any<PageQuery>(), Arg.Any<CancellationToken>())
            .Returns(PagedResult<ContactChannelAggregate>.Failure(repositoryError));

        var result = await CreateUseCase()
            .ExecuteAsync(new GetContactChannelsInputDto(IsActive: null, SearchName: null), new PageQuery(0, 10));

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Internal);
        result.Error.Origin.ShouldBe(
            "ContactChannelRepository",
            "the use case does not replace the origin of a failure it did not produce");
    }
}
