using ContactChannel.Application.Ports;
using ContactChannel.Application.UseCases.DeleteContactChannel;
using ContactChannel.Domain.Errors;
using ContactChannel.Domain.Repositories;
using NSubstitute;
using Shared.Application.Ports;
using Shared.Results;
using Shared.Results.Errors;
using Shouldly;
using Xunit;

namespace UnitTests.Contexts.ContactChannel.Application.UseCases.DeleteContactChannel;

public sealed class DeleteContactChannelUseCaseTests
{
    private const string RepositoryOrigin = "ContactChannelRepository";

    private readonly IContactChannelRepository _repository = Substitute.For<IContactChannelRepository>();
    private readonly IContactChannelUsageReader _usageReader = Substitute.For<IContactChannelUsageReader>();
    private readonly IUnitOfWorkPort _unitOfWork = Substitute.For<IUnitOfWorkPort>();

    private DeleteContactChannelUseCase CreateUseCase() => new(_repository, _usageReader, _unitOfWork);

    private void IsReferenced(bool referenced) =>
        _usageReader.IsReferencedAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Result<bool>.Success(referenced));

    private void RemoveReturns(Result result) =>
        _repository.RemoveAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(result);

    private void CommitReturns(Result result) =>
        _unitOfWork.CommitAsync(Arg.Any<CancellationToken>()).Returns(result);

    [Fact]
    public async Task ExecuteAsync_WhenTheChannelIsFree_RemovesItAndCommits()
    {
        IsReferenced(false);
        RemoveReturns(Result.Success());
        CommitReturns(Result.Success());

        var result = await CreateUseCase().ExecuteAsync(7);

        result.IsSuccess.ShouldBeTrue();
        await _repository.Received(1).RemoveAsync(7, Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenTheChannelIsReferenced_FailsAsInUse()
    {
        IsReferenced(true);

        var result = await CreateUseCase().ExecuteAsync(7);

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Conflict);
        result.Error.Message.ShouldContain("7");
        result.Error.Context.ShouldBe(ContactChannelErrors.Context);
        result.Error.Origin.ShouldBe(nameof(DeleteContactChannelUseCase));
    }

    [Fact]
    public async Task ExecuteAsync_WhenTheChannelIsReferenced_NeverTouchesTheDatabase()
    {
        IsReferenced(true);

        await CreateUseCase().ExecuteAsync(7);

        await _repository.DidNotReceive().RemoveAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_ChecksTheUsageOfTheSameIdentifierItWasGiven()
    {
        IsReferenced(false);
        RemoveReturns(Result.Success());
        CommitReturns(Result.Success());
        using var cancellation = new CancellationTokenSource();

        await CreateUseCase().ExecuteAsync(42, cancellation.Token);

        await _usageReader.Received(1).IsReferencedAsync(42, cancellation.Token);
    }

    [Fact]
    public async Task ExecuteAsync_WhenTheUsageCheckFails_PropagatesTheErrorAndStops()
    {
        _usageReader.IsReferencedAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Result<bool>.Failure(
                new InternalError("A persistence error occurred.") { Origin = "ContactChannelUsageReader" }));

        var result = await CreateUseCase().ExecuteAsync(7);

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Internal);
        result.Error.Origin.ShouldBe("ContactChannelUsageReader");
        await _repository.DidNotReceive().RemoveAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WithAnUnknownId_PropagatesTheNotFoundWithoutCommitting()
    {
        IsReferenced(false);
        RemoveReturns(ContactChannelErrors.NotFound(404) with { Origin = RepositoryOrigin });

        var result = await CreateUseCase().ExecuteAsync(404);

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.NotFound);
        result.Error.Origin.ShouldBe(RepositoryOrigin);
        await _unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    // Safety net for the race between the usage check and the write: the foreign key fires at
    // commit time and SqlServerErrorClassifier turns the 547 into a conflict.
    [Fact]
    public async Task ExecuteAsync_WhenTheForeignKeyFiresAtCommit_PropagatesTheTranslatedConflict()
    {
        IsReferenced(false);
        RemoveReturns(Result.Success());
        CommitReturns(new ConflictError("The operation conflicts with a related record.")
        {
            Origin = "UnitOfWorkAdapter",
        });

        var result = await CreateUseCase().ExecuteAsync(7);

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Conflict);
        result.Error.Origin.ShouldBe(
            "UnitOfWorkAdapter",
            "the translated 547 keeps the origin of the component that classified it");
    }
}
