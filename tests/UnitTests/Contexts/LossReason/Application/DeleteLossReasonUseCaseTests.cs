using LossReason.Application.Ports;
using LossReason.Application.UseCases.DeleteLossReason;
using LossReason.Domain.Repositories;
using NSubstitute;
using Shared.Application.Ports;
using Shared.Results;
using Shared.Results.Errors;
using Shouldly;
using Xunit;

namespace UnitTests.Contexts.LossReason.Application;

public sealed class DeleteLossReasonUseCaseTests
{
    private const int Id = 1;
    private const string RepositoryOrigin = "LossReasonRepository";
    private const string ReaderOrigin = "LossReasonUsageReader";

    private readonly ILossReasonRepository _repository = Substitute.For<ILossReasonRepository>();
    private readonly ILossReasonUsageReader _usageReader = Substitute.For<ILossReasonUsageReader>();
    private readonly IUnitOfWorkPort _unitOfWork = Substitute.For<IUnitOfWorkPort>();

    private DeleteLossReasonUseCase CreateSut() => new(_repository, _usageReader, _unitOfWork);

    private void ItIsFree() =>
        _usageReader.IsUsedAsync(Id, Arg.Any<CancellationToken>())
            .Returns(Result<bool>.Success(false));

    [Fact]
    public async Task ExecuteAsync_WhenTheReasonIsFree_RemovesItAndCommits()
    {
        ItIsFree();
        _repository.RemoveAsync(Id, Arg.Any<CancellationToken>()).Returns(Result.Success());
        _unitOfWork.CommitAsync(Arg.Any<CancellationToken>()).Returns(Result.Success());

        var result = await CreateSut().ExecuteAsync(Id, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        await _repository.Received(1).RemoveAsync(Id, Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WithAnIdThatIsNotThere_SucceedsWithoutCheckingExistence()
    {
        ItIsFree();
        // The repository deletes by id and reports success even when the row is not there.
        _repository.RemoveAsync(Id, Arg.Any<CancellationToken>()).Returns(Result.Success());
        _unitOfWork.CommitAsync(Arg.Any<CancellationToken>()).Returns(Result.Success());

        var result = await CreateSut().ExecuteAsync(Id, CancellationToken.None);

        // The delete is idempotent: a missing id is a 204, not a 404.
        result.IsSuccess.ShouldBeTrue();
        await _repository.DidNotReceive().ExistsAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenTheReasonIsInUse_ReturnsConflictWithoutRemoving()
    {
        _usageReader.IsUsedAsync(Id, Arg.Any<CancellationToken>())
            .Returns(Result<bool>.Success(true));

        var result = await CreateSut().ExecuteAsync(Id, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Conflict);

        // The FK is NO_ACTION: issuing the delete would fail with SQL Server error 547 (D7).
        await _repository.DidNotReceive().RemoveAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenTheReaderFails_KeepsTheReaderOrigin()
    {
        var readerError = new DomainError("A persistence error occurred.", ErrorType.Internal)
        {
            Origin = ReaderOrigin
        };
        _usageReader.IsUsedAsync(Id, Arg.Any<CancellationToken>())
            .Returns(Result<bool>.Failure(readerError));

        var result = await CreateSut().ExecuteAsync(Id, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Origin.ShouldBe(
            ReaderOrigin,
            "the use case does not replace the origin of the failure");
        await _repository.DidNotReceive().RemoveAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenRemoveFails_DoesNotCommitAndKeepsTheRepositoryOrigin()
    {
        ItIsFree();
        var removeError = new DomainError("A persistence error occurred.", ErrorType.Internal)
        {
            Origin = RepositoryOrigin
        };
        _repository.RemoveAsync(Id, Arg.Any<CancellationToken>()).Returns(Result.Failure(removeError));

        var result = await CreateSut().ExecuteAsync(Id, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Origin.ShouldBe(
            RepositoryOrigin,
            "the use case does not replace the origin of the failure");

        // A delete that failed must never reach the commit.
        await _unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }
}
