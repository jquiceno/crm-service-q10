using LossReason.Application.UseCases.CreateLossReason;
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

public sealed class CreateLossReasonUseCaseTests
{
    private const string ValidName = "Precio";

    private readonly ILossReasonRepository _repository = Substitute.For<ILossReasonRepository>();

    private CreateLossReasonUseCase CreateSut() => new(_repository);

    [Fact]
    public async Task ExecuteAsync_WithValidInput_PersistsOnceAndReturnsTheAssignedId()
    {
        // The repository rebuilds the aggregate from the inserted row, which is where the IDENTITY lands.
        _repository
            .CreateAsync(Arg.Any<LossReasonAggregate>(), Arg.Any<CancellationToken>())
            .Returns(LossReasonAggregate.Reconstruct(7, ValidName, isActive: true));

        // Never wired into the use case: CreateAsync owns the transaction (D3).
        var unitOfWork = Substitute.For<IUnitOfWorkPort>();

        var result = await CreateSut().ExecuteAsync(new CreateLossReasonInputDto(ValidName));

        result.IsSuccess.ShouldBeTrue();
        result.Value.Id.ShouldBe(7);
        result.Value.Name.ShouldBe(ValidName);
        result.Value.IsActive.ShouldBeTrue();

        await _repository.Received(1)
            .CreateAsync(Arg.Any<LossReasonAggregate>(), Arg.Any<CancellationToken>());
        await unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WithInvalidName_FailsInTheAggregateAndNeverPersists()
    {
        var result = await CreateSut().ExecuteAsync(new CreateLossReasonInputDto(Name: null));

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.DomainError);
        result.Error.Context.ShouldBe(LossReasonErrors.Context);
        result.Error.Origin.ShouldBe(nameof(CreateLossReasonUseCase));

        await _repository.DidNotReceive()
            .CreateAsync(Arg.Any<LossReasonAggregate>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenTheRepositoryFails_PropagatesItsOriginUntouched()
    {
        var failure = new DomainError("Persistence failure.", ErrorType.Internal)
        {
            Context = LossReasonErrors.Context,
            Origin = "LossReasonRepository"
        };

        _repository
            .CreateAsync(Arg.Any<LossReasonAggregate>(), Arg.Any<CancellationToken>())
            .Returns(Result<LossReasonAggregate>.Failure(failure));

        var result = await CreateSut().ExecuteAsync(new CreateLossReasonInputDto(ValidName));

        result.IsFailure.ShouldBeTrue();
        result.Error.Origin.ShouldBe("LossReasonRepository", "the use case does not replace the origin of the failure");
    }
}
