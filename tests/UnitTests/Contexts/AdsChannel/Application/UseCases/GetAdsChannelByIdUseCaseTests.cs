using AdsChannel.Application.UseCases.GetAdsChannelById;
using AdsChannel.Domain.Aggregates;
using AdsChannel.Domain.Errors;
using AdsChannel.Domain.Repositories;
using NSubstitute;
using Shouldly;
using Xunit;

namespace UnitTests.Contexts.AdsChannel.Application.UseCases;

public sealed class GetAdsChannelByIdUseCaseTests
{
    private const int Id = 1;
    private const string RepositoryOrigin = "AdsChannelRepository";

    private readonly IAdsChannelRepository _repository = Substitute.For<IAdsChannelRepository>();
    private readonly GetAdsChannelByIdUseCase _sut;

    public GetAdsChannelByIdUseCaseTests()
    {
        _sut = new GetAdsChannelByIdUseCase(_repository);
    }

    [Fact]
    public async Task ExecuteAsync_WithExistingId_ReturnsMappedOutputDto()
    {
        var aggregate = AdsChannelAggregate.Reconstruct(Id, "Google Ads", true);
        _repository.GetByIdAsync(Id, Arg.Any<CancellationToken>())
            .Returns(aggregate);

        var result = await _sut.ExecuteAsync(Id, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Id.ShouldBe(aggregate.Id);
        result.Value.Name.ShouldBe(aggregate.Name);
        result.Value.IsActive.ShouldBe(aggregate.IsActive);
    }

    [Fact]
    public async Task ExecuteAsync_WithNonexistentId_PropagatesTheRepositoryNotFoundError()
    {
        const int nonexistentId = 404;
        _repository.GetByIdAsync(nonexistentId, Arg.Any<CancellationToken>())
            .Returns(AdsChannelErrors.NotFound(nonexistentId) with { Origin = RepositoryOrigin });

        var result = await _sut.ExecuteAsync(nonexistentId, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Origin.ShouldBe(
            RepositoryOrigin, "the use case does not replace the origin of the failure");
    }
}
