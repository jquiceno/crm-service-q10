using BusinessStatus.Application.UseCases.UpdateBusinessStatus;
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

public sealed class UpdateBusinessStatusUseCaseTests
{
    private const int Id = 7;
    private const string RepositoryOrigin = "BusinessStatusRepository";
    private const string UnitOfWorkOrigin = "UnitOfWorkAdapter";

    private readonly IBusinessStatusRepository _repository = Substitute.For<IBusinessStatusRepository>();
    private readonly IUnitOfWorkPort _unitOfWork = Substitute.For<IUnitOfWorkPort>();
    private readonly UpdateBusinessStatusUseCase _sut;

    public UpdateBusinessStatusUseCaseTests()
    {
        _sut = new UpdateBusinessStatusUseCase(_repository, _unitOfWork);
        _repository.Update(Arg.Any<BusinessStatusAggregate>()).Returns(Result.Success());
        _unitOfWork.CommitAsync(Arg.Any<CancellationToken>()).Returns(Result.Success());
    }

    private static UpdateBusinessStatusInputDto Input(
        string? name = "Negotiation",
        decimal percentage = 50m,
        string? color = "49ff7c",
        bool isActive = true) =>
        new(name, percentage, color, isActive);

    private void RepositoryReturnsStored(
        int? percentage = 30, string name = "Prospecting", string? color = "cccccc", bool isActive = true) =>
        _repository
            .GetByIdAsync(Id, Arg.Any<CancellationToken>())
            .Returns(Result<BusinessStatusAggregate>.Success(
                BusinessStatusAggregate.Reconstruct(Id, name, percentage, color, isActive)));

    private async Task NothingWasPersistedAsync()
    {
        _repository.DidNotReceive().Update(Arg.Any<BusinessStatusAggregate>());
        await _unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>()).ConfigureAwait(false);
    }

    [Fact]
    public async Task ExecuteAsync_WithValidInput_ReplacesEveryFieldAndCommits()
    {
        RepositoryReturnsStored();

        var result = await _sut.ExecuteAsync(Id, Input(percentage: 60m, color: null, isActive: false));

        result.IsSuccess.ShouldBeTrue();
        result.Value.Id.ShouldBe(Id);
        result.Value.Name.ShouldBe("Negotiation");
        result.Value.Percentage.ShouldBe(60);
        result.Value.Color.ShouldBeNull();
        result.Value.IsActive.ShouldBeFalse();
        _repository.Received(1).Update(Arg.Is<BusinessStatusAggregate>(a => a.Id == Id));
        await _unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_TrimsTheNameBeforePersisting()
    {
        RepositoryReturnsStored();

        await _sut.ExecuteAsync(Id, Input(name: "  Negotiation  "));

        _repository.Received(1).Update(Arg.Is<BusinessStatusAggregate>(a => a.Name == "Negotiation"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    public async Task ExecuteAsync_MovingAnIntermediateToATerminalPercentage_FailsWithoutPersisting(int percentage)
    {
        RepositoryReturnsStored();

        var result = await _sut.ExecuteAsync(Id, Input(percentage: percentage));

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.DomainError);
        result.Error.Details.ShouldContain(detail => detail.Property == "Percentage");
        await NothingWasPersistedAsync();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    public async Task ExecuteAsync_OnATerminalStatus_AcceptsNameAndColourWhenThePercentageTravelsUnchanged(int stored)
    {
        RepositoryReturnsStored(percentage: stored);

        var result = await _sut.ExecuteAsync(Id, Input(name: "Renamed", percentage: stored, color: "49ff7c"));

        result.IsSuccess.ShouldBeTrue();
        result.Value.Name.ShouldBe("Renamed");
        result.Value.Percentage.ShouldBe(stored);
        result.Value.Color.ShouldBe("49ff7c");
        await _unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(0, 100)]
    [InlineData(100, 0)]
    [InlineData(100, 50)]
    public async Task ExecuteAsync_ChangingThePercentageOfATerminalStatus_FailsWithoutPersisting(
        int stored, int requested)
    {
        RepositoryReturnsStored(percentage: stored);

        var result = await _sut.ExecuteAsync(Id, Input(percentage: requested));

        result.IsFailure.ShouldBeTrue();
        result.Error.Details.ShouldContain(detail =>
            detail.Property == "Percentage"
            && detail.Errors!.Contains(BusinessStatusErrors.TerminalPercentageIsImmutable.Message));
        await NothingWasPersistedAsync();
    }

    [Fact]
    public async Task ExecuteAsync_WithEmptyNameAndTerminalPercentage_ReturnsBothErrors()
    {
        RepositoryReturnsStored();

        var result = await _sut.ExecuteAsync(Id, Input(name: "   ", percentage: 100m));

        result.IsFailure.ShouldBeTrue();
        var properties = result.Error.Details.Select(detail => detail.Property).ToList();
        properties.Count.ShouldBe(2);
        properties.ShouldContain("Name");
        properties.ShouldContain("Percentage");
        await NothingWasPersistedAsync();
    }

    [Fact]
    public async Task ExecuteAsync_WithNonIntegerPercentage_FailsWithoutPersisting()
    {
        RepositoryReturnsStored();

        var result = await _sut.ExecuteAsync(Id, Input(percentage: 50.5m));

        result.IsFailure.ShouldBeTrue();
        result.Error.Details.ShouldContain(detail => detail.Property == "Percentage");
        await NothingWasPersistedAsync();
    }

    [Fact]
    public async Task ExecuteAsync_WithInvalidColor_FailsWithoutPersisting()
    {
        RepositoryReturnsStored();

        var result = await _sut.ExecuteAsync(Id, Input(color: "zzzzzz"));

        result.IsFailure.ShouldBeTrue();
        result.Error.Details.ShouldContain(detail => detail.Property == "Color");
        await NothingWasPersistedAsync();
    }

    [Fact]
    public async Task ExecuteAsync_WhenTheDomainFails_SealsTheErrorWithContextAndOrigin()
    {
        RepositoryReturnsStored();

        var result = await _sut.ExecuteAsync(Id, Input(percentage: 100m));

        result.Error.Context.ShouldBe(BusinessStatusErrors.Context);
        result.Error.Origin.ShouldBe(nameof(UpdateBusinessStatusUseCase));
    }

    [Fact]
    public async Task ExecuteAsync_WhenTheStatusDoesNotExist_PropagatesTheNotFoundWithoutPersisting()
    {
        _repository
            .GetByIdAsync(Id, Arg.Any<CancellationToken>())
            .Returns(Result<BusinessStatusAggregate>.Failure(
                BusinessStatusErrors.NotFound(Id) with
                {
                    Context = BusinessStatusErrors.Context,
                    Origin = RepositoryOrigin
                }));

        var result = await _sut.ExecuteAsync(Id, Input());

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.NotFound);
        result.Error.Origin.ShouldBe(RepositoryOrigin, "the use case does not replace the origin of the failure");
        await NothingWasPersistedAsync();
    }

    [Fact]
    public async Task ExecuteAsync_WhenTheRepositoryUpdateFails_PropagatesTheErrorWithoutCommitting()
    {
        RepositoryReturnsStored();
        _repository
            .Update(Arg.Any<BusinessStatusAggregate>())
            .Returns(Result.Failure(new DomainError("boom", ErrorType.Internal)
            {
                Origin = RepositoryOrigin
            }));

        var result = await _sut.ExecuteAsync(Id, Input());

        result.IsFailure.ShouldBeTrue();
        result.Error.Origin.ShouldBe(RepositoryOrigin);
        await _unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenTheCommitFails_PropagatesTheErrorUntouched()
    {
        RepositoryReturnsStored();
        _unitOfWork
            .CommitAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Failure(new DomainError("commit failed", ErrorType.Internal)
            {
                Context = BusinessStatusErrors.Context,
                Origin = UnitOfWorkOrigin
            }));

        var result = await _sut.ExecuteAsync(Id, Input());

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Internal);
        result.Error.Origin.ShouldBe(UnitOfWorkOrigin, "the use case does not replace the origin of the failure");
    }
}
