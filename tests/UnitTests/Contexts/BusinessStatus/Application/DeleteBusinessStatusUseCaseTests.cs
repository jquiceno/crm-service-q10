using BusinessStatus.Application.UseCases.DeleteBusinessStatus;
using BusinessStatus.Domain.Aggregates;
using BusinessStatus.Domain.Errors;
using BusinessStatus.Domain.Repositories;
using NSubstitute;
using Shared.Application.Ports;
using Shared.Results;
using Shared.Results.Errors;
using Shouldly;
using Xunit;

namespace UnitTests.Contexts.BusinessStatus.Application;

public sealed class DeleteBusinessStatusUseCaseTests
{
    private const int Id = 7;
    private const string RepositoryOrigin = "BusinessStatusRepository";
    private const string UnitOfWorkOrigin = "UnitOfWorkAdapter";

    private readonly IBusinessStatusRepository _repository = Substitute.For<IBusinessStatusRepository>();
    private readonly IUnitOfWorkPort _unitOfWork = Substitute.For<IUnitOfWorkPort>();
    private readonly DeleteBusinessStatusUseCase _sut;

    public DeleteBusinessStatusUseCaseTests() => _sut = new DeleteBusinessStatusUseCase(_repository, _unitOfWork);

    private void RepositoryReturns(int? percentage)
    {
        _repository
            .GetByIdAsync(Id, Arg.Any<CancellationToken>())
            .Returns(Result<BusinessStatusAggregate>.Success(
                BusinessStatusAggregate.Reconstruct(Id, "Negotiation", percentage, "49ff7c", isActive: true)));

        _repository.RemoveAsync(Id, Arg.Any<CancellationToken>()).Returns(Result.Success());
        _unitOfWork.CommitAsync(Arg.Any<CancellationToken>()).Returns(Result.Success());
    }

    [Fact]
    public async Task ExecuteAsync_WithAnIntermediateStatus_RemovesItAndCommits()
    {
        RepositoryReturns(percentage: 50);

        var result = await _sut.ExecuteAsync(Id);

        result.IsSuccess.ShouldBeTrue();
        await _repository.Received(1).RemoveAsync(Id, Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WithoutPercentage_RemovesIt()
    {
        RepositoryReturns(percentage: null);

        var result = await _sut.ExecuteAsync(Id);

        result.IsSuccess.ShouldBeTrue("a row with a null percentage is not terminal");
        await _repository.Received(1).RemoveAsync(Id, Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    public async Task ExecuteAsync_WithATerminalStatus_ConflictsWithoutTouchingThePersistence(int percentage)
    {
        RepositoryReturns(percentage);

        var result = await _sut.ExecuteAsync(Id);

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Conflict);
        result.Error.ShouldBe(BusinessStatusErrors.TerminalCannotBeDeleted);
        await _repository.DidNotReceive().RemoveAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenTheStatusIsTerminal_SealsTheErrorWithContextAndOrigin()
    {
        RepositoryReturns(percentage: BusinessStatusAggregate.MaxPercentage);

        var result = await _sut.ExecuteAsync(Id);

        result.Error.Context.ShouldBe(BusinessStatusErrors.Context);
        result.Error.Origin.ShouldBe(nameof(DeleteBusinessStatusUseCase));
    }

    [Fact]
    public async Task ExecuteAsync_WithAnUnknownId_PropagatesTheNotFoundUntouched()
    {
        var notFound = BusinessStatusErrors.NotFound(Id) with
        {
            Context = BusinessStatusErrors.Context,
            Origin = RepositoryOrigin
        };
        _repository
            .GetByIdAsync(Id, Arg.Any<CancellationToken>())
            .Returns(Result<BusinessStatusAggregate>.Failure(notFound));

        var result = await _sut.ExecuteAsync(Id);

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.NotFound);
        result.Error.Origin.ShouldBe(RepositoryOrigin, "the use case does not replace the origin of the failure");
        await _repository.DidNotReceive().RemoveAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenTheRemovalFails_PropagatesTheErrorWithoutCommitting()
    {
        RepositoryReturns(percentage: 50);
        var failure = new DomainError("boom", ErrorType.Internal)
        {
            Context = BusinessStatusErrors.Context,
            Origin = RepositoryOrigin
        };
        _repository.RemoveAsync(Id, Arg.Any<CancellationToken>()).Returns(Result.Failure(failure));

        var result = await _sut.ExecuteAsync(Id);

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Internal);
        result.Error.Origin.ShouldBe(RepositoryOrigin);
        await _unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenTheCommitConflicts_PropagatesTheConflictUntouched()
    {
        RepositoryReturns(percentage: 50);
        var conflict = new ConflictError("The DELETE statement conflicted with a reference constraint.")
        {
            Origin = UnitOfWorkOrigin
        };
        _unitOfWork.CommitAsync(Arg.Any<CancellationToken>()).Returns(Result.Failure(conflict));

        var result = await _sut.ExecuteAsync(Id);

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Conflict);
        result.Error.Origin.ShouldBe(
            UnitOfWorkOrigin, "the 409 raised by the foreign key arrives already classified");
    }

    [Fact]
    public async Task ExecuteAsync_ForwardsTheCancellationToken()
    {
        RepositoryReturns(percentage: 50);
        using var cancellation = new CancellationTokenSource();

        await _sut.ExecuteAsync(Id, cancellation.Token);

        await _repository.Received(1).GetByIdAsync(Id, cancellation.Token);
        await _repository.Received(1).RemoveAsync(Id, cancellation.Token);
        await _unitOfWork.Received(1).CommitAsync(cancellation.Token);
    }
}
