using System.Text.Json;
using Api.Controllers;
using LossReason.Application.UseCases.CreateLossReason;
using LossReason.Application.UseCases.DeleteLossReason;
using LossReason.Application.UseCases.GetLossReasonById;
using LossReason.Application.UseCases.GetLossReasons;
using LossReason.Application.UseCases.UpdateLossReason;
using LossReason.Domain.Errors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.AspNetCore.Routing;
using NSubstitute;
using Shared.Application.Dtos;
using Shared.Domain.Pagination;
using Shared.Results;
using Shouldly;
using Xunit;

namespace UnitTests.Api.Controllers;

/// <summary>
/// The controller delegates and does not decide: every test asserts that the action forwards its
/// arguments untouched and that the success status code comes from the HttpXResult it returns.
/// Failure paths are here only to prove the controller does not inspect the Result — the status
/// code is derived from the ErrorType the use case produced.
/// </summary>
public sealed class LossReasonsControllerTests
{
    private readonly IGetLossReasonsUseCase _getLossReasons = Substitute.For<IGetLossReasonsUseCase>();
    private readonly IGetLossReasonByIdUseCase _getLossReasonById = Substitute.For<IGetLossReasonByIdUseCase>();
    private readonly ICreateLossReasonUseCase _createLossReason = Substitute.For<ICreateLossReasonUseCase>();
    private readonly IUpdateLossReasonUseCase _updateLossReason = Substitute.For<IUpdateLossReasonUseCase>();
    private readonly IDeleteLossReasonUseCase _deleteLossReason = Substitute.For<IDeleteLossReasonUseCase>();

    private LossReasonsController CreateSut() => new(
        _getLossReasons,
        _getLossReasonById,
        _createLossReason,
        _updateLossReason,
        _deleteLossReason);

    private static async Task<(int StatusCode, string Body)> ExecuteAsync(IActionResult result)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();
        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());

        await result.ExecuteResultAsync(actionContext).ConfigureAwait(false);

        httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(httpContext.Response.Body);
        var body = await reader.ReadToEndAsync().ConfigureAwait(false);
        return (httpContext.Response.StatusCode, body);
    }

    [Fact]
    public void GetLossReasons_DoesNotRestrictTheCacheKeyToASubsetOfTheQuery()
    {
        var cache = typeof(LossReasonsController)
            .GetMethod(nameof(LossReasonsController.GetLossReasons))!
            .GetCustomAttributes(typeof(OutputCacheAttribute), inherit: false)
            .Cast<OutputCacheAttribute>()
            .Single();

        // An annotated endpoint already varies by the whole query string: the attribute reapplies
        // DefaultPolicy after the base policy and restores QueryKeys = "*" (cache.md, "Cómo se arma
        // la clave de caché"). Declaring VaryByQueryKeys *restricts* the key to the listed names,
        // which is R8: a filter left out of the list makes the listing serve the result of one
        // search for another, answering 200 with no error anywhere. It happened on this branch when
        // the filter was renamed to Search and the list still said "name".
        cache.VaryByQueryKeys.ShouldBeNull();
        cache.Tags.ShouldBe(["loss-reasons"]);
        cache.Duration.ShouldBe(3 * 24 * 60 * 60);
    }

    [Fact]
    public async Task GetLossReasons_WhenUseCaseSucceeds_Returns200WithItemsAndTotalCount()
    {
        var filter = new GetLossReasonsInputDto("Price", IsActive: true);
        _getLossReasons.ExecuteAsync(filter, Arg.Any<PageQuery>(), Arg.Any<CancellationToken>())
            .Returns(PagedResult<GetLossReasonsOutputDto>.Success(
                [new GetLossReasonsOutputDto(1, "Price", true)],
                totalCount: 8));

        var result = await CreateSut().GetLossReasons(filter, new PageQueryInputDto(), CancellationToken.None);
        var (statusCode, body) = await ExecuteAsync(result);

        statusCode.ShouldBe(StatusCodes.Status200OK);
        using var json = JsonDocument.Parse(body);
        var data = json.RootElement.GetProperty("data");
        data.GetProperty("totalCount").GetInt32().ShouldBe(8);
        data.GetProperty("items")[0].GetProperty("name").GetString().ShouldBe("Price");
    }

    [Fact]
    public async Task GetLossReasons_ForwardsTheFilterAndTheRequestedPage()
    {
        var filter = new GetLossReasonsInputDto("Price", IsActive: null);
        _getLossReasons.ExecuteAsync(Arg.Any<GetLossReasonsInputDto>(), Arg.Any<PageQuery>(), Arg.Any<CancellationToken>())
            .Returns(PagedResult<GetLossReasonsOutputDto>.Success([], totalCount: 0));

        await CreateSut().GetLossReasons(filter, new PageQueryInputDto(PageIndex: 2, PageSize: 30), CancellationToken.None);

        // The action builds the PageQuery from the query DTO; if it swapped the two ints or defaulted
        // them, the listing would silently page over the wrong window.
        await _getLossReasons.Received(1).ExecuteAsync(
            filter,
            Arg.Is<PageQuery>(p => p.PageIndex == 2 && p.PageSize == 30),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetLossReasons_WhenCatalogIsEmpty_Returns200WithAnEmptyList()
    {
        _getLossReasons.ExecuteAsync(Arg.Any<GetLossReasonsInputDto>(), Arg.Any<PageQuery>(), Arg.Any<CancellationToken>())
            .Returns(PagedResult<GetLossReasonsOutputDto>.Success([], totalCount: 0));

        var result = await CreateSut().GetLossReasons(
            new GetLossReasonsInputDto(null, null), new PageQueryInputDto(), CancellationToken.None);
        var (statusCode, body) = await ExecuteAsync(result);

        // D9: an empty catalog is a 200 with an empty list, never a 404.
        statusCode.ShouldBe(StatusCodes.Status200OK);
        using var json = JsonDocument.Parse(body);
        json.RootElement.GetProperty("data").GetProperty("items").GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task GetLossReasonById_WhenFound_Returns200AndForwardsTheId()
    {
        _getLossReasonById.ExecuteAsync(7, Arg.Any<CancellationToken>())
            .Returns(Result<GetLossReasonByIdOutputDto>.Success(new GetLossReasonByIdOutputDto(7, "Price", true)));

        var result = await CreateSut().GetLossReasonById(new ConsecutiveIdInputDto(7), CancellationToken.None);
        var (statusCode, body) = await ExecuteAsync(result);

        statusCode.ShouldBe(StatusCodes.Status200OK);
        JsonDocument.Parse(body).RootElement.GetProperty("data").GetProperty("id").GetInt32().ShouldBe(7);
        await _getLossReasonById.Received(1).ExecuteAsync(7, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetLossReasonById_WhenUseCaseReturnsNotFound_Returns404()
    {
        _getLossReasonById.ExecuteAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Result<GetLossReasonByIdOutputDto>.Failure(LossReasonErrors.NotFound(7)));

        var result = await CreateSut().GetLossReasonById(new ConsecutiveIdInputDto(7), CancellationToken.None);
        var (statusCode, _) = await ExecuteAsync(result);

        statusCode.ShouldBe(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task CreateLossReason_WhenUseCaseSucceeds_Returns201AndForwardsTheInput()
    {
        var input = new CreateLossReasonInputDto("Price", IsActive: true);
        _createLossReason.ExecuteAsync(input, Arg.Any<CancellationToken>())
            .Returns(Result<CreateLossReasonOutputDto>.Success(new CreateLossReasonOutputDto(1, "Price", true)));

        var result = await CreateSut().CreateLossReason(input, CancellationToken.None);
        var (statusCode, body) = await ExecuteAsync(result);

        statusCode.ShouldBe(StatusCodes.Status201Created);
        JsonDocument.Parse(body).RootElement.GetProperty("data").GetProperty("id").GetInt32().ShouldBe(1);
        await _createLossReason.Received(1).ExecuteAsync(input, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateLossReason_WhenUseCaseSucceeds_Returns200AndForwardsIdAndInput()
    {
        var input = new UpdateLossReasonInputDto("High price", IsActive: false);
        _updateLossReason.ExecuteAsync(3, input, Arg.Any<CancellationToken>())
            .Returns(Result<UpdateLossReasonOutputDto>.Success(new UpdateLossReasonOutputDto(3, "High price", false)));

        var result = await CreateSut().UpdateLossReason(new ConsecutiveIdInputDto(3), input, CancellationToken.None);
        var (statusCode, body) = await ExecuteAsync(result);

        statusCode.ShouldBe(StatusCodes.Status200OK);
        JsonDocument.Parse(body).RootElement.GetProperty("data").GetProperty("name").GetString().ShouldBe("High price");
        await _updateLossReason.Received(1).ExecuteAsync(3, input, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateLossReason_WhenUseCaseReturnsNotFound_Returns404()
    {
        _updateLossReason.ExecuteAsync(Arg.Any<int>(), Arg.Any<UpdateLossReasonInputDto>(), Arg.Any<CancellationToken>())
            .Returns(Result<UpdateLossReasonOutputDto>.Failure(LossReasonErrors.NotFound(3)));

        var result = await CreateSut().UpdateLossReason(
            new ConsecutiveIdInputDto(3), new UpdateLossReasonInputDto("Price", IsActive: true), CancellationToken.None);
        var (statusCode, _) = await ExecuteAsync(result);

        statusCode.ShouldBe(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task DeleteLossReason_WhenUseCaseSucceeds_Returns204WithoutBody()
    {
        _deleteLossReason.ExecuteAsync(5, Arg.Any<CancellationToken>()).Returns(Result.Success());

        var result = await CreateSut().DeleteLossReason(new ConsecutiveIdInputDto(5), CancellationToken.None);
        var (statusCode, body) = await ExecuteAsync(result);

        statusCode.ShouldBe(StatusCodes.Status204NoContent);
        body.ShouldBeEmpty();
        await _deleteLossReason.Received(1).ExecuteAsync(5, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteLossReason_WhenReasonIsInUse_Returns409()
    {
        _deleteLossReason.ExecuteAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure(LossReasonErrors.InUse(5)));

        var result = await CreateSut().DeleteLossReason(new ConsecutiveIdInputDto(5), CancellationToken.None);
        var (statusCode, _) = await ExecuteAsync(result);

        // D7: the 409 is a decision of the use case, which consulted the usage reader. The controller
        // only maps the ErrorType it received.
        statusCode.ShouldBe(StatusCodes.Status409Conflict);
    }
}
