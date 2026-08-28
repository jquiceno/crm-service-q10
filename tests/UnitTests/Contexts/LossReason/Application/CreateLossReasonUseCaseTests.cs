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
    private const int AssignedId = 7;

    private readonly ILossReasonRepository _repository = Substitute.For<ILossReasonRepository>();

    /// <summary>Aggregate the use case handed to the repository, captured by <see cref="StubSuccessfulCreate"/>.</summary>
    private LossReasonAggregate? _persisted;

    private CreateLossReasonUseCase CreateSut() => new(_repository);

    // The repository rebuilds the aggregate from the inserted row, which is where the IDENTITY
    // lands. The stub echoes back what it received instead of fixed values: that way the output
    // assertions still fail if the mapping drops or swaps a field on the way in.
    private void StubSuccessfulCreate() =>
        _repository
            .CreateAsync(Arg.Do<LossReasonAggregate>(a => _persisted = a), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var incoming = call.Arg<LossReasonAggregate>();
                return LossReasonAggregate.Reconstruct(AssignedId, incoming.Name, incoming.IsActive);
            });

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ExecuteAsync_WithValidInput_PersistsAnAggregateBuiltFromTheInput(bool isActive)
    {
        StubSuccessfulCreate();
        var input = new CreateLossReasonInputDto(ValidName, isActive);

        var result = await CreateSut().ExecuteAsync(input);

        result.IsSuccess.ShouldBeTrue();

        await _repository.Received(1)
            .CreateAsync(Arg.Any<LossReasonAggregate>(), Arg.Any<CancellationToken>());

        _persisted.ShouldNotBeNull();
        _persisted.Name.ShouldBe(input.Name);
        _persisted.IsActive.ShouldBe(input.IsActive!.Value);
        _persisted.Id.ShouldBe(0, "the id is the IDENTITY, so it is only assigned by the insert");
        _persisted.CreatedAt.ShouldNotBeNull("Create() stamps the aggregate before it is persisted");
    }

    [Fact]
    public async Task ExecuteAsync_WithNullIsActive_FailsInTheAggregateAndNeverPersists()
    {
        // IsActive is nullable so the structural validator can report it; the aggregate refuses it
        // too, so a caller that skips the HTTP layer cannot create a loss reason without the flag.
        var result = await CreateSut().ExecuteAsync(new CreateLossReasonInputDto(ValidName, IsActive: null));

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.DomainError);
        result.Error.Origin.ShouldBe(nameof(CreateLossReasonUseCase));
        result.Error.Details
            .SelectMany(detail => detail.Errors ?? [])
            .ShouldContain(LossReasonErrors.IsActiveRequired.Message);

        await _repository.DidNotReceive()
            .CreateAsync(Arg.Any<LossReasonAggregate>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ExecuteAsync_WithValidInput_ReturnsThePersistedRowWithItsAssignedId(bool isActive)
    {
        StubSuccessfulCreate();
        var input = new CreateLossReasonInputDto(ValidName, isActive);

        var result = await CreateSut().ExecuteAsync(input);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Id.ShouldBe(AssignedId);
        result.Value.Name.ShouldBe(input.Name);
        result.Value.IsActive.ShouldBe(input.IsActive!.Value);
    }

    [Fact]
    public void TheUseCase_DoesNotDependOnTheUnitOfWork()
    {
        // D3: creation commits inside the repository, never through the unit of work. That
        // guarantee is structural -- the use case does not take the port at all -- so the assert
        // reads the constructors. A DidNotReceive() on a substitute nothing injects cannot fail.
        typeof(CreateLossReasonUseCase)
            .GetConstructors()
            .SelectMany(c => c.GetParameters())
            .ShouldNotContain(
                p => p.ParameterType == typeof(IUnitOfWorkPort),
                "the create use case must not depend on IUnitOfWorkPort");
    }

    [Fact]
    public async Task ExecuteAsync_WithInvalidName_FailsInTheAggregateAndNeverPersists()
    {
        var result = await CreateSut().ExecuteAsync(new CreateLossReasonInputDto(Name: null, IsActive: true));

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

        var result = await CreateSut().ExecuteAsync(new CreateLossReasonInputDto(ValidName, IsActive: true));

        result.IsFailure.ShouldBeTrue();
        result.Error.Origin.ShouldBe("LossReasonRepository", "the use case does not replace the origin of the failure");
    }
}
