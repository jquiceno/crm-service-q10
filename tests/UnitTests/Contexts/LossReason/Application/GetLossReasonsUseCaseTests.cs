using LossReason.Application.UseCases.GetLossReasons;
using LossReason.Domain.Aggregates;
using LossReason.Domain.Queries;
using LossReason.Domain.Repositories;
using NSubstitute;
using Shared.Domain.Pagination;
using Shared.Results;
using Shared.Results.Errors;
using Shouldly;
using Xunit;

namespace UnitTests.Contexts.LossReason.Application;

public sealed class GetLossReasonsUseCaseTests
{
    private const string RepositoryOrigin = "LossReasonRepository";

    private readonly ILossReasonRepository _repository = Substitute.For<ILossReasonRepository>();

    private static PageQuery FirstPage => new(pageIndex: 0, pageSize: 20);

    private GetLossReasonsUseCase CreateSut() => new(_repository);

    [Fact]
    public async Task ExecuteAsync_WithFilter_PassesItToTheRepository()
    {
        var page = FirstPage;
        _repository
            .GetAsync(Arg.Any<LossReasonFilter>(), Arg.Any<PageQuery>(), Arg.Any<CancellationToken>())
            .Returns(PagedResult<LossReasonAggregate>.Success([], 0));

        await CreateSut().ExecuteAsync(new GetLossReasonsInputDto("Precio", IsActive: true), page);

        await _repository.Received(1).GetAsync(
            Arg.Is<LossReasonFilter>(filter => filter.Name == "Precio" && filter.IsActive == true),
            page,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WithRows_MapsItemsAndTotalCount()
    {
        _repository
            .GetAsync(Arg.Any<LossReasonFilter>(), Arg.Any<PageQuery>(), Arg.Any<CancellationToken>())
            .Returns(PagedResult<LossReasonAggregate>.Success(
                [
                    LossReasonAggregate.Reconstruct(1, "Precio", isActive: true),
                    LossReasonAggregate.Reconstruct(2, "Competencia", isActive: false)
                ],
                8));

        var result = await CreateSut().ExecuteAsync(new GetLossReasonsInputDto(null, null), FirstPage);

        result.IsSuccess.ShouldBeTrue();
        result.TotalCount.ShouldBe(8);
        result.Items.Count.ShouldBe(2);
        result.Items[0].ShouldBe(new GetLossReasonsOutputDto(1, "Precio", IsActive: true));
        result.Items[1].ShouldBe(new GetLossReasonsOutputDto(2, "Competencia", IsActive: false));
    }

    [Fact]
    public async Task ExecuteAsync_WithNoRows_ReturnsSuccessfulEmptyPage()
    {
        _repository
            .GetAsync(Arg.Any<LossReasonFilter>(), Arg.Any<PageQuery>(), Arg.Any<CancellationToken>())
            .Returns(PagedResult<LossReasonAggregate>.Success([], 0));

        var result = await CreateSut().ExecuteAsync(new GetLossReasonsInputDto(null, null), FirstPage);

        // An empty catalogue is a 200 with an empty page, never a 404 (D9).
        result.IsSuccess.ShouldBeTrue();
        result.Items.ShouldBeEmpty();
        result.TotalCount.ShouldBe(0);
    }

    [Fact]
    public async Task ExecuteAsync_WhenRepositoryFails_KeepsTheRepositoryOrigin()
    {
        var repositoryError = new DomainError("A persistence error occurred.", ErrorType.Internal)
        {
            Origin = RepositoryOrigin
        };
        _repository
            .GetAsync(Arg.Any<LossReasonFilter>(), Arg.Any<PageQuery>(), Arg.Any<CancellationToken>())
            .Returns(PagedResult<LossReasonAggregate>.Failure(repositoryError));

        var result = await CreateSut().ExecuteAsync(new GetLossReasonsInputDto(null, null), FirstPage);

        result.IsFailure.ShouldBeTrue();
        result.Error.Origin.ShouldBe(
            RepositoryOrigin,
            "the use case does not replace the origin of the failure");
        result.Error.Type.ShouldBe(ErrorType.Internal);
    }
}
