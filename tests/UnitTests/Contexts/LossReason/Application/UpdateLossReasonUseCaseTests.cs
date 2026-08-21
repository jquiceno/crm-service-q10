using LossReason.Application.UseCases.UpdateLossReason;
using LossReason.Domain.Aggregates;
using LossReason.Domain.Errors;
using LossReason.Domain.Repositories;
using NSubstitute;
using Shared.Application.Ports;
using Shared.Results;
using Shared.Results.Errors;
using Shouldly;
using Xunit;

namespace UnitTests.Contexts.LossReason.Application;

public sealed class UpdateLossReasonUseCaseTests
{
    private const int ExistingId = 7;
    private const string ValidName = "Precio";
    private const string RepositoryOrigin = "LossReasonRepository";
    private const string UnitOfWorkOrigin = "UnitOfWorkAdapter";

    private readonly ILossReasonRepository _repository = Substitute.For<ILossReasonRepository>();
    private readonly IUnitOfWorkPort _unitOfWork = Substitute.For<IUnitOfWorkPort>();

    private UpdateLossReasonUseCase CreateSut() => new(_repository, _unitOfWork);

    private void GivenTheRowExists() =>
        _repository
            .GetByIdAsync(ExistingId, Arg.Any<CancellationToken>())
            .Returns(LossReasonAggregate.Reconstruct(ExistingId, "Competencia", isActive: true));

    [Fact]
    public async Task ExecuteAsync_WithValidInput_UpdatesTheAggregateAndCommitsOnce()
    {
        GivenTheRowExists();
        _repository.Update(Arg.Any<LossReasonAggregate>()).Returns(Result.Success());
        _unitOfWork.CommitAsync(Arg.Any<CancellationToken>()).Returns(Result.Success());

        var result = await CreateSut()
            .ExecuteAsync(ExistingId, new UpdateLossReasonInputDto(ValidName, IsActive: false));

        result.IsSuccess.ShouldBeTrue();
        result.Value.Id.ShouldBe(ExistingId);
        result.Value.Name.ShouldBe(ValidName);
        result.Value.IsActive.ShouldBeFalse();

        _repository.Received(1).Update(Arg.Any<LossReasonAggregate>());
        await _unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WithMissingId_ReturnsNotFoundWithoutCommitting()
    {
        _repository
            .GetByIdAsync(404, Arg.Any<CancellationToken>())
            .Returns(LossReasonErrors.NotFound(404) with { Origin = RepositoryOrigin });

        var result = await CreateSut()
            .ExecuteAsync(404, new UpdateLossReasonInputDto(ValidName, IsActive: true));

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.NotFound);
        result.Error.Origin.ShouldBe(RepositoryOrigin, "the use case does not replace the origin of the failure");

        _repository.DidNotReceive().Update(Arg.Any<LossReasonAggregate>());
        await _unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WithInvalidName_FailsInTheAggregateAndDoesNotPersist()
    {
        GivenTheRowExists();

        var result = await CreateSut()
            .ExecuteAsync(ExistingId, new UpdateLossReasonInputDto(Name: null, IsActive: true));

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.DomainError);
        result.Error.Context.ShouldBe(LossReasonErrors.Context);
        result.Error.Origin.ShouldBe(nameof(UpdateLossReasonUseCase));

        _repository.DidNotReceive().Update(Arg.Any<LossReasonAggregate>());
        await _unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenTheCommitFails_PropagatesTheUnitOfWorkOrigin()
    {
        GivenTheRowExists();
        _repository.Update(Arg.Any<LossReasonAggregate>()).Returns(Result.Success());
        _unitOfWork
            .CommitAsync(Arg.Any<CancellationToken>())
            .Returns(new DomainError("Commit failure.", ErrorType.Internal) { Origin = UnitOfWorkOrigin });

        var result = await CreateSut()
            .ExecuteAsync(ExistingId, new UpdateLossReasonInputDto(ValidName, IsActive: true));

        result.IsFailure.ShouldBeTrue();
        result.Error.Origin.ShouldBe(UnitOfWorkOrigin, "the use case does not replace the origin of the failure");
    }
}
