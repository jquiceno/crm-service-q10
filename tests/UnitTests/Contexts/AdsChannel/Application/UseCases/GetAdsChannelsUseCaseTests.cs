using AdsChannel.Application.UseCases.GetAdsChannels;
using AdsChannel.Domain.Aggregates;
using AdsChannel.Domain.Queries;
using AdsChannel.Domain.Repositories;
using NSubstitute;
using Shared.Domain.Pagination;
using Shared.Results;
using Shared.Results.Errors;
using Shouldly;
using Xunit;

namespace UnitTests.Contexts.AdsChannel.Application.UseCases;

public sealed class GetAdsChannelsUseCaseTests
{
    private readonly IAdsChannelRepository _repository = Substitute.For<IAdsChannelRepository>();
    private readonly GetAdsChannelsUseCase _sut;

    public GetAdsChannelsUseCaseTests()
    {
        _sut = new GetAdsChannelsUseCase(_repository);
    }

    [Fact]
    public async Task ExecuteAsync_WithMatchingItems_ReturnsSuccessWithMappedItemsAndTotalCount()
    {
        var input = new GetAdsChannelsInputDto(NameContains: "ads", IsActive: true);
        var page = new PageQuery(0, 20);

        var first = AdsChannelAggregate.Reconstruct(1, "Google Ads", true);
        var second = AdsChannelAggregate.Reconstruct(2, "Meta Ads", true);

        _repository
            .GetAsync(Arg.Any<AdsChannelFilter>(), page, Arg.Any<CancellationToken>())
            .Returns(PagedResult<AdsChannelAggregate>.Success([first, second], totalCount: 5));

        var result = await _sut.ExecuteAsync(input, page, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Items.Count.ShouldBe(2);
        result.Items[0].Id.ShouldBe(1);
        result.Items[0].Name.ShouldBe("Google Ads");
        result.Items[0].IsActive.ShouldBeTrue();
        result.Items[1].Id.ShouldBe(2);
        result.Items[1].Name.ShouldBe("Meta Ads");
        result.TotalCount.ShouldBe(5);

        await _repository.Received(1).GetAsync(
            Arg.Is<AdsChannelFilter>(f => f.NameContains == "ads" && f.IsActive == true),
            page,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WithNoMatches_ReturnsSuccessWithEmptyItemsAndZeroTotalCount()
    {
        var input = new GetAdsChannelsInputDto(NameContains: "nonexistent", IsActive: null);
        var page = new PageQuery(0, 20);

        _repository
            .GetAsync(Arg.Any<AdsChannelFilter>(), page, Arg.Any<CancellationToken>())
            .Returns(PagedResult<AdsChannelAggregate>.Success([], totalCount: 0));

        var result = await _sut.ExecuteAsync(input, page, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Items.ShouldBeEmpty();
        result.TotalCount.ShouldBe(0);
    }

    [Fact]
    public async Task ExecuteAsync_WhenRepositoryFails_PropagatesTheRepositoryOrigin()
    {
        var input = new GetAdsChannelsInputDto(NameContains: null, IsActive: null);
        var page = new PageQuery(0, 20);

        var error = new DomainError("boom", ErrorType.Internal) { Origin = "AdsChannelRepository" };
        _repository
            .GetAsync(Arg.Any<AdsChannelFilter>(), page, Arg.Any<CancellationToken>())
            .Returns(PagedResult<AdsChannelAggregate>.Failure(error));

        var result = await _sut.ExecuteAsync(input, page, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Message.ShouldBe("boom");
        result.Error.Origin.ShouldBe("AdsChannelRepository", "the use case does not replace the origin of the failure");
    }
}
