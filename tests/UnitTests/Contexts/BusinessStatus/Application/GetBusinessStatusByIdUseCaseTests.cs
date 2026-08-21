using BusinessStatus.Application.UseCases.GetBusinessStatusById;
using BusinessStatus.Domain.Aggregates;
using BusinessStatus.Domain.Errors;
using BusinessStatus.Domain.Repositories;
using NSubstitute;
using Shared.Results;
using Shared.Results.Errors;
using Shouldly;
using Xunit;

namespace UnitTests.Contexts.BusinessStatus.Application;

public sealed class GetBusinessStatusByIdUseCaseTests
{
    private const string RepositoryOrigin = "BusinessStatusRepository";

    private readonly IBusinessStatusRepository _repository = Substitute.For<IBusinessStatusRepository>();

    private GetBusinessStatusByIdUseCase Sut => new(_repository);

    private void Returns(BusinessStatusAggregate aggregate) =>
        _repository.GetByIdAsync(aggregate.Id, Arg.Any<CancellationToken>())
            .Returns(Result<BusinessStatusAggregate>.Success(aggregate));

    [Fact]
    public async Task ExecuteAsync_WithAnExistingId_MapsTheAggregateToItsOutputDto()
    {
        Returns(BusinessStatusAggregate.Reconstruct(7, "Negotiation", 50, "49ff7c", isActive: true));

        var result = await Sut.ExecuteAsync(7);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Id.ShouldBe(7);
        result.Value.Name.ShouldBe("Negotiation");
        result.Value.Percentage.ShouldBe(50);
        result.Value.Color.ShouldBe("49ff7c");
        result.Value.IsActive.ShouldBeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_KeepsThePercentageAndTheColourRaw()
    {
        Returns(BusinessStatusAggregate.Reconstruct(7, "Legacy row", percentage: null, color: null, isActive: false));

        var result = await Sut.ExecuteAsync(7);

        result.Value.Percentage.ShouldBeNull("a row without percentage is served as it is, never as 0");
        result.Value.Color.ShouldBeNull("the legacy CCCCCC default is not resolved by this contract");
        result.Value.IsActive.ShouldBeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    public async Task ExecuteAsync_ServesATerminalStatusLikeAnyOther(int percentage)
    {
        Returns(BusinessStatusAggregate.Reconstruct(7, "Terminal", percentage, null, isActive: true));

        var result = await Sut.ExecuteAsync(7);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Percentage.ShouldBe(percentage);
    }

    [Fact]
    public async Task ExecuteAsync_WithAnUnknownId_PropagatesTheNotFoundOfTheRepository()
    {
        _repository.GetByIdAsync(999, Arg.Any<CancellationToken>())
            .Returns(Result<BusinessStatusAggregate>.Failure(
                BusinessStatusErrors.NotFound(999) with
                {
                    Context = BusinessStatusErrors.Context,
                    Origin = RepositoryOrigin
                }));

        var result = await Sut.ExecuteAsync(999);

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.NotFound);
        result.Error.Context.ShouldBe(BusinessStatusErrors.Context);
        result.Error.Origin.ShouldBe(RepositoryOrigin, "the use case does not replace the origin of the failure");
    }

    [Fact]
    public async Task ExecuteAsync_WhenTheQueryFails_PropagatesThePersistenceErrorUntouched()
    {
        _repository.GetByIdAsync(7, Arg.Any<CancellationToken>())
            .Returns(Result<BusinessStatusAggregate>.Failure(
                new InternalError("A persistence error occurred.") { Origin = RepositoryOrigin }));

        var result = await Sut.ExecuteAsync(7);

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Internal);
        result.Error.Origin.ShouldBe(RepositoryOrigin);
    }

    [Fact]
    public async Task ExecuteAsync_ForwardsTheIdAndTheTokenToTheRepository()
    {
        using var cts = new CancellationTokenSource();
        Returns(BusinessStatusAggregate.Reconstruct(31, "Negotiation", 50, null, isActive: true));

        await Sut.ExecuteAsync(31, cts.Token);

        await _repository.Received(1).GetByIdAsync(31, cts.Token);
    }
}
