using AdsChannel.Application.UseCases.CreateAdsChannel;
using AdsChannel.Domain.Aggregates;
using AdsChannel.Domain.Repositories;
using Infrastructure.Persistence.EntityFramework.Common;
using NSubstitute;
using Shared.Results;
using Shared.Results.Errors;
using Shouldly;
using Xunit;

namespace UnitTests.Contexts.AdsChannel.Application.UseCases;

public sealed class CreateAdsChannelUseCaseTests
{
    private readonly IAdsChannelRepository _repository = Substitute.For<IAdsChannelRepository>();
    private readonly CreateAdsChannelUseCase _sut;

    public CreateAdsChannelUseCaseTests()
    {
        _sut = new CreateAdsChannelUseCase(_repository);
    }

    [Fact]
    public async Task ExecuteAsync_WithValidInput_PersistsAggregateAndReturnsSuccess()
    {
        var input = new CreateAdsChannelInputDto("Google Ads");
        _repository.ExistsByNameAsync(input.Name!, cancellationToken: Arg.Any<CancellationToken>())
            .Returns(Result<bool>.Success(false));
        _repository.CreateAsync(Arg.Any<AdsChannelAggregate>(), Arg.Any<CancellationToken>())
            .Returns(args => Result<AdsChannelAggregate>.Success(
                AdsChannelAggregate.Reconstruct(1, ((AdsChannelAggregate)args[0]).Name, ((AdsChannelAggregate)args[0]).IsActive)));

        var result = await _sut.ExecuteAsync(input, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Id.ShouldBe(1);
        result.Value.Name.ShouldBe(input.Name);
        result.Value.IsActive.ShouldBe(input.IsActive);

        await _repository.Received(1).ExistsByNameAsync(input.Name!, cancellationToken: Arg.Any<CancellationToken>());
        await _repository.Received(1).CreateAsync(Arg.Any<AdsChannelAggregate>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenNameAlreadyExists_ReturnsConflictWithUseCaseOrigin()
    {
        var input = new CreateAdsChannelInputDto("Google Ads");
        _repository.ExistsByNameAsync(input.Name!, cancellationToken: Arg.Any<CancellationToken>())
            .Returns(Result<bool>.Success(true));

        var result = await _sut.ExecuteAsync(input, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Conflict);
        result.Error.Origin.ShouldBe(nameof(CreateAdsChannelUseCase));

        await _repository.DidNotReceive().CreateAsync(Arg.Any<AdsChannelAggregate>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task ExecuteAsync_WithEmptyName_ReturnsValidationErrorWithUseCaseOrigin(string? name)
    {
        var input = new CreateAdsChannelInputDto(name);

        var result = await _sut.ExecuteAsync(input, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Origin.ShouldBe(nameof(CreateAdsChannelUseCase));

        await _repository.DidNotReceive().ExistsByNameAsync(Arg.Any<string>(), cancellationToken: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WithNameOverMaxLength_ReturnsValidationErrorWithUseCaseOrigin()
    {
        var input = new CreateAdsChannelInputDto(new string('a', AdsChannelAggregate.MaxNameLength + 1));

        var result = await _sut.ExecuteAsync(input, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Origin.ShouldBe(nameof(CreateAdsChannelUseCase));

        await _repository.DidNotReceive().ExistsByNameAsync(Arg.Any<string>(), cancellationToken: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenRepositoryCreateFails_PropagatesTheRepositoryOrigin()
    {
        var input = new CreateAdsChannelInputDto("Google Ads");
        _repository.ExistsByNameAsync(input.Name!, cancellationToken: Arg.Any<CancellationToken>())
            .Returns(Result<bool>.Success(false));
        _repository.CreateAsync(Arg.Any<AdsChannelAggregate>(), Arg.Any<CancellationToken>())
            .Returns(Result<AdsChannelAggregate>.Failure(PersistenceErrors.Failure("AdsChannelRepository")));

        var result = await _sut.ExecuteAsync(input, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Origin.ShouldBe(
            "AdsChannelRepository", "the use case does not replace the origin of the failure");
    }

    [Fact]
    public async Task ExecuteAsync_WhenExistsByNameFails_PropagatesTheRepositoryOrigin()
    {
        var input = new CreateAdsChannelInputDto("Google Ads");
        _repository.ExistsByNameAsync(input.Name!, cancellationToken: Arg.Any<CancellationToken>())
            .Returns(Result<bool>.Failure(PersistenceErrors.Failure("AdsChannelRepository")));

        var result = await _sut.ExecuteAsync(input, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Origin.ShouldBe(
            "AdsChannelRepository", "the use case does not replace the origin of the failure");

        await _repository.DidNotReceive().CreateAsync(Arg.Any<AdsChannelAggregate>(), Arg.Any<CancellationToken>());
    }
}
