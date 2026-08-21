using AdsChannel.Application.UseCases.UpdateAdsChannel;
using AdsChannel.Domain.Aggregates;
using AdsChannel.Domain.Errors;
using AdsChannel.Domain.Repositories;
using Infrastructure.Persistence.EntityFramework.Common;
using NSubstitute;
using Shared.Application.Ports;
using Shared.Results;
using Shared.Results.Errors;
using Shouldly;
using Xunit;

namespace UnitTests.Contexts.AdsChannel.Application.UseCases;

public sealed class UpdateAdsChannelUseCaseTests
{
    private const int Id = 1;

    private readonly IAdsChannelRepository _repository = Substitute.For<IAdsChannelRepository>();
    private readonly IUnitOfWorkPort _unitOfWork = Substitute.For<IUnitOfWorkPort>();
    private readonly UpdateAdsChannelUseCase _sut;

    public UpdateAdsChannelUseCaseTests()
    {
        _sut = new UpdateAdsChannelUseCase(_repository, _unitOfWork);
    }

    [Fact]
    public async Task ExecuteAsync_WithValidInput_UpdatesAggregateAndReturnsSuccess()
    {
        var input = new UpdateAdsChannelInputDto("New name", false);
        var aggregate = AdsChannelAggregate.Reconstruct(Id, "Old name", true);
        _repository.GetByIdAsync(Id, Arg.Any<CancellationToken>())
            .Returns(Result<AdsChannelAggregate>.Success(aggregate));
        _repository.ExistsByNameAsync(input.Name!, excludingId: Id, cancellationToken: Arg.Any<CancellationToken>())
            .Returns(Result<bool>.Success(false));
        _repository.Update(aggregate).Returns(Result.Success());
        _unitOfWork.CommitAsync(Arg.Any<CancellationToken>()).Returns(Result.Success());

        var result = await _sut.ExecuteAsync(Id, input, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Id.ShouldBe(Id);
        result.Value.Name.ShouldBe("New name");
        result.Value.IsActive.ShouldBeFalse();

        await _repository.Received(1).GetByIdAsync(Id, Arg.Any<CancellationToken>());
        await _repository.Received(1)
            .ExistsByNameAsync(input.Name!, excludingId: Id, cancellationToken: Arg.Any<CancellationToken>());
        _repository.Received(1).Update(aggregate);
        await _unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenAdsChannelNotFound_PropagatesTheRepositoryOrigin()
    {
        var input = new UpdateAdsChannelInputDto("New name", true);
        _repository.GetByIdAsync(Id, Arg.Any<CancellationToken>())
            .Returns(Result<AdsChannelAggregate>.Failure(
                AdsChannelErrors.NotFound(Id) with { Origin = "AdsChannelRepository" }));

        var result = await _sut.ExecuteAsync(Id, input, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.NotFound);
        result.Error.Origin.ShouldBe(
            "AdsChannelRepository", "the use case does not replace the origin of the failure");

        await _repository.DidNotReceive().ExistsByNameAsync(
            Arg.Any<string>(), excludingId: Arg.Any<int?>(), cancellationToken: Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ExecuteAsync_WithEmptyName_ReturnsValidationErrorWithUseCaseOrigin(string? name)
    {
        var input = new UpdateAdsChannelInputDto(name, true);
        var aggregate = AdsChannelAggregate.Reconstruct(Id, "Old name", true);
        _repository.GetByIdAsync(Id, Arg.Any<CancellationToken>())
            .Returns(Result<AdsChannelAggregate>.Success(aggregate));

        var result = await _sut.ExecuteAsync(Id, input, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Validation);
        result.Error.Origin.ShouldBe(nameof(UpdateAdsChannelUseCase));

        await _repository.DidNotReceive().ExistsByNameAsync(
            Arg.Any<string>(), excludingId: Arg.Any<int?>(), cancellationToken: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WithNameOverMaxLength_ReturnsValidationErrorWithUseCaseOrigin()
    {
        var input = new UpdateAdsChannelInputDto(new string('a', AdsChannelAggregate.MaxNameLength + 1), true);
        var aggregate = AdsChannelAggregate.Reconstruct(Id, "Old name", true);
        _repository.GetByIdAsync(Id, Arg.Any<CancellationToken>())
            .Returns(Result<AdsChannelAggregate>.Success(aggregate));

        var result = await _sut.ExecuteAsync(Id, input, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Validation);
        result.Error.Origin.ShouldBe(nameof(UpdateAdsChannelUseCase));

        await _repository.DidNotReceive().ExistsByNameAsync(
            Arg.Any<string>(), excludingId: Arg.Any<int?>(), cancellationToken: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenNameAlreadyExistsOnAnotherRecord_ReturnsConflictWithUseCaseOrigin()
    {
        var input = new UpdateAdsChannelInputDto("Duplicate name", true);
        var aggregate = AdsChannelAggregate.Reconstruct(Id, "Old name", true);
        _repository.GetByIdAsync(Id, Arg.Any<CancellationToken>())
            .Returns(Result<AdsChannelAggregate>.Success(aggregate));
        _repository.ExistsByNameAsync(input.Name!, excludingId: Id, cancellationToken: Arg.Any<CancellationToken>())
            .Returns(Result<bool>.Success(true));

        var result = await _sut.ExecuteAsync(Id, input, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Conflict);
        result.Error.Origin.ShouldBe(nameof(UpdateAdsChannelUseCase));

        _repository.DidNotReceive().Update(Arg.Any<AdsChannelAggregate>());
        await _unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenExistsByNameFails_PropagatesTheRepositoryOrigin()
    {
        var input = new UpdateAdsChannelInputDto("New name", true);
        var aggregate = AdsChannelAggregate.Reconstruct(Id, "Old name", true);
        _repository.GetByIdAsync(Id, Arg.Any<CancellationToken>())
            .Returns(Result<AdsChannelAggregate>.Success(aggregate));
        _repository.ExistsByNameAsync(input.Name!, excludingId: Id, cancellationToken: Arg.Any<CancellationToken>())
            .Returns(Result<bool>.Failure(PersistenceErrors.Failure("AdsChannelRepository")));

        var result = await _sut.ExecuteAsync(Id, input, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Origin.ShouldBe(
            "AdsChannelRepository", "the use case does not replace the origin of the failure");

        _repository.DidNotReceive().Update(Arg.Any<AdsChannelAggregate>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenRepositoryUpdateFails_PropagatesTheRepositoryOrigin()
    {
        var input = new UpdateAdsChannelInputDto("New name", true);
        var aggregate = AdsChannelAggregate.Reconstruct(Id, "Old name", true);
        _repository.GetByIdAsync(Id, Arg.Any<CancellationToken>())
            .Returns(Result<AdsChannelAggregate>.Success(aggregate));
        _repository.ExistsByNameAsync(input.Name!, excludingId: Id, cancellationToken: Arg.Any<CancellationToken>())
            .Returns(Result<bool>.Success(false));
        _repository.Update(aggregate).Returns(PersistenceErrors.Failure("AdsChannelRepository"));

        var result = await _sut.ExecuteAsync(Id, input, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Origin.ShouldBe(
            "AdsChannelRepository", "the use case does not replace the origin of the failure");

        await _unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenCommitFails_PropagatesTheUnitOfWorkOrigin()
    {
        var input = new UpdateAdsChannelInputDto("New name", true);
        var aggregate = AdsChannelAggregate.Reconstruct(Id, "Old name", true);
        _repository.GetByIdAsync(Id, Arg.Any<CancellationToken>())
            .Returns(Result<AdsChannelAggregate>.Success(aggregate));
        _repository.ExistsByNameAsync(input.Name!, excludingId: Id, cancellationToken: Arg.Any<CancellationToken>())
            .Returns(Result<bool>.Success(false));
        _repository.Update(aggregate).Returns(Result.Success());
        _unitOfWork.CommitAsync(Arg.Any<CancellationToken>())
            .Returns(PersistenceErrors.Failure("UnitOfWorkAdapter"));

        var result = await _sut.ExecuteAsync(Id, input, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Origin.ShouldBe(
            "UnitOfWorkAdapter", "the use case does not replace the origin of the failure");
    }
}
