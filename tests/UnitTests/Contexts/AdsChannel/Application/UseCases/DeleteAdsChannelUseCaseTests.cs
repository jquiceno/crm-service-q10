using AdsChannel.Application.UseCases.DeleteAdsChannel;
using AdsChannel.Domain.Errors;
using AdsChannel.Domain.Repositories;
using NSubstitute;
using Shared.Application.Ports;
using Shared.Results;
using Shared.Results.Errors;
using Shouldly;
using Xunit;

namespace UnitTests.Contexts.AdsChannel.Application.UseCases;

public sealed class DeleteAdsChannelUseCaseTests
{
    private const int Id = 42;

    private readonly IAdsChannelRepository _repository = Substitute.For<IAdsChannelRepository>();
    private readonly IUnitOfWorkPort _unitOfWork = Substitute.For<IUnitOfWorkPort>();
    private readonly DeleteAdsChannelUseCase _sut;

    public DeleteAdsChannelUseCaseTests()
    {
        _sut = new DeleteAdsChannelUseCase(_repository, _unitOfWork);
    }

    [Fact]
    public async Task ExecuteAsync_WithExistingId_RemovesAndCommits_ReturnsSuccess()
    {
        _repository.RemoveAsync(Id, Arg.Any<CancellationToken>()).Returns(Result.Success());
        _unitOfWork.CommitAsync(Arg.Any<CancellationToken>()).Returns(Result.Success());

        var result = await _sut.ExecuteAsync(Id, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();

        await _repository.Received(1).RemoveAsync(Id, Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenIdNotFound_PropagatesRepositoryNotFoundError_WithoutCommitting()
    {
        var notFound = AdsChannelErrors.NotFound(Id) with { Origin = "AdsChannelRepository" };
        _repository.RemoveAsync(Id, Arg.Any<CancellationToken>()).Returns(notFound);

        var result = await _sut.ExecuteAsync(Id, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Origin.ShouldBe(
            "AdsChannelRepository", "the use case does not replace the origin of the failure");
        result.Error.ShouldBeOfType<NotFoundError>();

        await _unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenCommitFails_PropagatesUnitOfWorkError_WithoutReplacingOrigin()
    {
        _repository.RemoveAsync(Id, Arg.Any<CancellationToken>()).Returns(Result.Success());

        var commitFailure = new InternalError("Commit failed.") with { Origin = "UnitOfWorkAdapter" };
        _unitOfWork.CommitAsync(Arg.Any<CancellationToken>()).Returns(commitFailure);

        var result = await _sut.ExecuteAsync(Id, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Origin.ShouldBe(
            "UnitOfWorkAdapter", "the use case does not replace the origin of the failure");

        await _repository.Received(1).RemoveAsync(Id, Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }
}
