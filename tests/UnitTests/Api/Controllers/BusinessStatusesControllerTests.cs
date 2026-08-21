using System.Text.Json;
using Api.Controllers;
using BusinessStatus.Application.UseCases.CreateBusinessStatus;
using BusinessStatus.Application.UseCases.GetBusinessStatuses;
using BusinessStatus.Domain.Enums;
using BusinessStatus.Application.UseCases.UpdateBusinessStatus;
using BusinessStatus.Domain.Errors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Routing;
using NSubstitute;
using Shared.Application.Dtos;
using Shared.Domain.Pagination;
using Shared.Results;
using Shared.Results.Errors;
using Shouldly;
using Xunit;

namespace UnitTests.Api.Controllers;

public sealed class BusinessStatusesControllerTests
{
    private readonly ICreateBusinessStatusUseCase _createBusinessStatusUseCase =
        Substitute.For<ICreateBusinessStatusUseCase>();

    private readonly IUpdateBusinessStatusUseCase _updateBusinessStatusUseCase =
        Substitute.For<IUpdateBusinessStatusUseCase>();

    private readonly IGetBusinessStatusesUseCase _getBusinessStatusesUseCase =
        Substitute.For<IGetBusinessStatusesUseCase>();

    private BusinessStatusesController Sut =>
        new(_createBusinessStatusUseCase, _getBusinessStatusesUseCase, _updateBusinessStatusUseCase);

    private static async Task<(int StatusCode, JsonDocument Body)> ExecuteAsync(IActionResult result)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();
        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());

        await result.ExecuteResultAsync(actionContext).ConfigureAwait(false);

        httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(httpContext.Response.Body);
        var json = await reader.ReadToEndAsync().ConfigureAwait(false);
        return (httpContext.Response.StatusCode, JsonDocument.Parse(json));
    }

    // ── GET /business-statuses ────────────────────────────────────────────────

    [Fact]
    public async Task GetBusinessStatuses_WhenTheUseCaseSucceeds_ReturnsOkWithItemsAndTotalCount()
    {
        var filter = new GetBusinessStatusesInputDto();
        _getBusinessStatusesUseCase
            .ExecuteAsync(filter, Arg.Any<PageQuery>(), Arg.Any<CancellationToken>())
            .Returns(PagedResult<GetBusinessStatusesOutputDto>.Success(
                [new GetBusinessStatusesOutputDto(7, "Negotiation", 50, "49ff7c", true)],
                totalCount: 12));

        var result = await Sut.GetBusinessStatuses(filter, new PageQueryInputDto(), CancellationToken.None);
        var (statusCode, body) = await ExecuteAsync(result);

        statusCode.ShouldBe(StatusCodes.Status200OK);
        var data = body.RootElement.GetProperty("data");
        data.GetProperty("totalCount").GetInt32().ShouldBe(12);
        var item = data.GetProperty("items")[0];
        item.GetProperty("id").GetInt32().ShouldBe(7);
        item.GetProperty("percentage").GetInt32().ShouldBe(50);
        item.GetProperty("color").GetString().ShouldBe("49ff7c");
    }

    [Fact]
    public async Task GetBusinessStatuses_PassesTheFilterAndThePageStraightToTheUseCase()
    {
        var filter = new GetBusinessStatusesInputDto("Nego", IsActive: true, BusinessStatusKind.Intermediate);
        _getBusinessStatusesUseCase
            .ExecuteAsync(Arg.Any<GetBusinessStatusesInputDto>(), Arg.Any<PageQuery>(), Arg.Any<CancellationToken>())
            .Returns(PagedResult<GetBusinessStatusesOutputDto>.Success([], totalCount: 0));

        await Sut.GetBusinessStatuses(
            filter,
            new PageQueryInputDto(PageIndex: 2, PageSize: 50),
            CancellationToken.None);

        await _getBusinessStatusesUseCase.Received(1).ExecuteAsync(
            filter,
            Arg.Is<PageQuery>(p => p.PageIndex == 2 && p.PageSize == 50),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetBusinessStatuses_WhenTheQueryFails_ReturnsInternalServerError()
    {
        var filter = new GetBusinessStatusesInputDto();
        _getBusinessStatusesUseCase
            .ExecuteAsync(filter, Arg.Any<PageQuery>(), Arg.Any<CancellationToken>())
            .Returns(PagedResult<GetBusinessStatusesOutputDto>.Failure(
                new DomainError("boom", ErrorType.Internal)));

        var result = await Sut.GetBusinessStatuses(filter, new PageQueryInputDto(), CancellationToken.None);
        var (statusCode, _) = await ExecuteAsync(result);

        statusCode.ShouldBe(StatusCodes.Status500InternalServerError);
    }

    // ── POST /business-statuses ───────────────────────────────────────────────

    [Fact]
    public async Task CreateBusinessStatus_WhenTheUseCaseSucceeds_ReturnsCreatedWithTheResource()
    {
        var input = new CreateBusinessStatusInputDto("Negotiation", 50m, "49ff7c");
        var output = new CreateBusinessStatusOutputDto(7, "Negotiation", 50, "49ff7c", true);
        _createBusinessStatusUseCase.ExecuteAsync(input, Arg.Any<CancellationToken>())
            .Returns(Result<CreateBusinessStatusOutputDto>.Success(output));

        var result = await Sut.CreateBusinessStatus(input, CancellationToken.None);
        var (statusCode, body) = await ExecuteAsync(result);

        statusCode.ShouldBe(StatusCodes.Status201Created);
        var data = body.RootElement.GetProperty("data");
        data.GetProperty("id").GetInt32().ShouldBe(7);
        data.GetProperty("name").GetString().ShouldBe("Negotiation");
        data.GetProperty("percentage").GetInt32().ShouldBe(50);
        await _createBusinessStatusUseCase.Received(1).ExecuteAsync(input, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateBusinessStatus_WhenTheDomainRejectsTheInput_ReturnsBadRequest()
    {
        var input = new CreateBusinessStatusInputDto("Won", 100m, null);
        _createBusinessStatusUseCase.ExecuteAsync(input, Arg.Any<CancellationToken>())
            .Returns(Result<CreateBusinessStatusOutputDto>.Failure(
                DomainError.FromValidationDomainErrors([BusinessStatusErrors.TerminalPercentageNotAllowed])));

        var result = await Sut.CreateBusinessStatus(input, CancellationToken.None);
        var (statusCode, _) = await ExecuteAsync(result);

        statusCode.ShouldBe(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task CreateBusinessStatus_WhenPersistenceFails_ReturnsInternalServerError()
    {
        var input = new CreateBusinessStatusInputDto("Negotiation", 50m, null);
        _createBusinessStatusUseCase.ExecuteAsync(input, Arg.Any<CancellationToken>())
            .Returns(Result<CreateBusinessStatusOutputDto>.Failure(
                new DomainError("boom", ErrorType.Internal)));

        var result = await Sut.CreateBusinessStatus(input, CancellationToken.None);
        var (statusCode, _) = await ExecuteAsync(result);

        statusCode.ShouldBe(StatusCodes.Status500InternalServerError);
    }

    [Fact]
    public async Task UpdateBusinessStatus_WhenTheUseCaseSucceeds_ReturnsOkWithTheResource()
    {
        var input = new UpdateBusinessStatusInputDto("Negotiation", 50m, "49ff7c");
        var output = new UpdateBusinessStatusOutputDto(7, "Negotiation", 50, "49ff7c", true);
        _updateBusinessStatusUseCase.ExecuteAsync(7, input, Arg.Any<CancellationToken>())
            .Returns(Result<UpdateBusinessStatusOutputDto>.Success(output));

        var result = await Sut.UpdateBusinessStatus(7, input, CancellationToken.None);
        var (statusCode, body) = await ExecuteAsync(result);

        statusCode.ShouldBe(StatusCodes.Status200OK);
        var data = body.RootElement.GetProperty("data");
        data.GetProperty("id").GetInt32().ShouldBe(7);
        data.GetProperty("name").GetString().ShouldBe("Negotiation");
        data.GetProperty("percentage").GetInt32().ShouldBe(50);
        data.GetProperty("color").GetString().ShouldBe("49ff7c");
        data.GetProperty("isActive").GetBoolean().ShouldBeTrue();
        await _updateBusinessStatusUseCase.Received(1).ExecuteAsync(7, input, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateBusinessStatus_WhenTheDomainRejectsTheInput_ReturnsBadRequest()
    {
        var input = new UpdateBusinessStatusInputDto("Won", 100m, null);
        _updateBusinessStatusUseCase.ExecuteAsync(7, input, Arg.Any<CancellationToken>())
            .Returns(Result<UpdateBusinessStatusOutputDto>.Failure(
                DomainError.FromValidationDomainErrors([BusinessStatusErrors.TerminalPercentageIsImmutable])));

        var result = await Sut.UpdateBusinessStatus(7, input, CancellationToken.None);
        var (statusCode, _) = await ExecuteAsync(result);

        statusCode.ShouldBe(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task UpdateBusinessStatus_WhenTheStatusDoesNotExist_ReturnsNotFound()
    {
        var input = new UpdateBusinessStatusInputDto("Negotiation", 50m, null);
        _updateBusinessStatusUseCase.ExecuteAsync(404, input, Arg.Any<CancellationToken>())
            .Returns(Result<UpdateBusinessStatusOutputDto>.Failure(BusinessStatusErrors.NotFound(404)));

        var result = await Sut.UpdateBusinessStatus(404, input, CancellationToken.None);
        var (statusCode, _) = await ExecuteAsync(result);

        statusCode.ShouldBe(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task UpdateBusinessStatus_WhenPersistenceFails_ReturnsInternalServerError()
    {
        var input = new UpdateBusinessStatusInputDto("Negotiation", 50m, null);
        _updateBusinessStatusUseCase.ExecuteAsync(7, input, Arg.Any<CancellationToken>())
            .Returns(Result<UpdateBusinessStatusOutputDto>.Failure(
                new DomainError("boom", ErrorType.Internal)));

        var result = await Sut.UpdateBusinessStatus(7, input, CancellationToken.None);
        var (statusCode, _) = await ExecuteAsync(result);

        statusCode.ShouldBe(StatusCodes.Status500InternalServerError);
    }
}
