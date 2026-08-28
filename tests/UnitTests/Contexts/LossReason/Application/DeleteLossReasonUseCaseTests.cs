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

    private void ItExists() =>
        _repository.ExistsAsync(Id, Arg.Any<CancellationToken>())
            .Returns(Result<bool>.Success(true));

    [Fact]
    public async Task ExecuteAsync_WhenTheReasonIsFree_RemovesItAndCommits()
    {
        ItExists();
        _usageReader.IsUsedAsync(Id, Arg.Any<CancellationToken>())
            .Returns(Result<bool>.Success(false));
        _repository.RemoveAsync(Id, Arg.Any<CancellationToken>()).Returns(Result.Success());
        _unitOfWork.CommitAsync(Arg.Any<CancellationToken>()).Returns(Result.Success());

        var result = await CreateSut().ExecuteAsync(Id, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        await _repository.Received(1).RemoveAsync(Id, Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenTheReasonIsInUse_ReturnsConflictWithoutRemoving()
    {
        ItExists();
        _usageReader.IsUsedAsync(Id, Arg.Any<CancellationToken>())
            .Returns(Result<bool>.Success(true));

        var result = await CreateSut().ExecuteAsync(Id, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Conflict);

        // The FK is NO_ACTION: staging the delete would fail with SQL Server error 547 (D7).
        await _repository.DidNotReceive().RemoveAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WithNonExistentId_ReturnsNotFoundWithoutQueryingTheReader()
    {
        _repository.ExistsAsync(Id, Arg.Any<CancellationToken>())
            .Returns(Result<bool>.Success(false));

        var result = await CreateSut().ExecuteAsync(Id, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.NotFound);

        // Existence is checked first on purpose: neg_cau_consecutivo is not indexed, so consulting
        // the reader here would make every 404 pay a scan of ~300.000 rows (D7, risk R2).
        await _usageReader.DidNotReceive().IsUsedAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
        await _repository.DidNotReceive().RemoveAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenTheReaderFails_KeepsTheReaderOrigin()
    {
        ItExists();
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
    public async Task ExecuteAsync_WhenRemoveFails_DoesNotCommit()
    {
        ItExists();
        _usageReader.IsUsedAsync(Id, Arg.Any<CancellationToken>())
            .Returns(Result<bool>.Success(false));
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

        // A staged delete that failed must never reach the commit.
        await _unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenTheRepositoryFails_KeepsTheRepositoryOrigin()
    {
        var repositoryError = new DomainError("A persistence error occurred.", ErrorType.Internal)
        {
            Origin = RepositoryOrigin
        };
        _repository.ExistsAsync(Id, Arg.Any<CancellationToken>())
            .Returns(Result<bool>.Failure(repositoryError));

        var result = await CreateSut().ExecuteAsync(Id, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Origin.ShouldBe(
            RepositoryOrigin,
            "the use case does not replace the origin of the failure");
        await _usageReader.DidNotReceive().IsUsedAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
    }
}
