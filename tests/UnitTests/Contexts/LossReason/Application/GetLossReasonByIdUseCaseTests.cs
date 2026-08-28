using LossReason.Application.UseCases.GetLossReasonById;
using LossReason.Domain.Aggregates;
using LossReason.Domain.Errors;
using LossReason.Domain.Repositories;
using NSubstitute;
using Shared.Results;
using Shared.Results.Errors;
using Shouldly;
using Xunit;

namespace UnitTests.Contexts.LossReason.Application;

public sealed class GetLossReasonByIdUseCaseTests
{
    private const int Id = 1;
    private const string RepositoryOrigin = "LossReasonRepository";

    private readonly ILossReasonRepository _repository = Substitute.For<ILossReasonRepository>();
    private readonly GetLossReasonByIdUseCase _sut;

    public GetLossReasonByIdUseCaseTests()
    {
        _sut = new GetLossReasonByIdUseCase(_repository);
    }

    [Fact]
    public async Task ExecuteAsync_WithExistentId_ReturnsMappedOutputDto()
    {
        var aggregate = LossReasonAggregate.Reconstruct(Id, "Precio", true);
        _repository.GetByIdAsync(Id, Arg.Any<CancellationToken>())
            .Returns(Result<LossReasonAggregate>.Success(aggregate));

        var result = await _sut.ExecuteAsync(Id, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Id.ShouldBe(Id);
        result.Value.Name.ShouldBe("Precio");
        result.Value.IsActive.ShouldBeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_WithNonExistentId_ReturnsNotFound()
    {
        _repository.GetByIdAsync(Id, Arg.Any<CancellationToken>())
            .Returns(Result<LossReasonAggregate>.Failure(LossReasonErrors.NotFound(Id)));

        var result = await _sut.ExecuteAsync(Id, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.NotFound);
    }

    [Fact]
    public async Task ExecuteAsync_WhenRepositoryFails_PropagatesTheRepositoryOrigin()
    {
        var repositoryError = new DomainError("A persistence error occurred.", ErrorType.Internal)
        {
            Origin = RepositoryOrigin
        };
        _repository.GetByIdAsync(Id, Arg.Any<CancellationToken>())
            .Returns(Result<LossReasonAggregate>.Failure(repositoryError));

        var result = await _sut.ExecuteAsync(Id, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Origin.ShouldBe(
            RepositoryOrigin,
            "the use case does not replace the origin of the failure");
        result.Error.Type.ShouldBe(ErrorType.Internal);
    }
}
