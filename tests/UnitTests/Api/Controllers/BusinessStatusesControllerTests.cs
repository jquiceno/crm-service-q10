using System.Text.Json;
using Api.Controllers;
using BusinessStatus.Application.UseCases.CreateBusinessStatus;
using BusinessStatus.Application.UseCases.DeleteBusinessStatus;
using BusinessStatus.Application.UseCases.GetBusinessStatusById;
using BusinessStatus.Application.UseCases.UpdateBusinessStatus;
using BusinessStatus.Domain.Errors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Routing;
using NSubstitute;
using Shared.Results;
using Shared.Results.Errors;
using Shouldly;
using Xunit;

namespace UnitTests.Api.Controllers;

public sealed class BusinessStatusesControllerTests
{
    private const int Id = 7;

    private readonly ICreateBusinessStatusUseCase _createBusinessStatusUseCase =
        Substitute.For<ICreateBusinessStatusUseCase>();

    private readonly IGetBusinessStatusByIdUseCase _getBusinessStatusByIdUseCase =
        Substitute.For<IGetBusinessStatusByIdUseCase>();

    private readonly IUpdateBusinessStatusUseCase _updateBusinessStatusUseCase =
        Substitute.For<IUpdateBusinessStatusUseCase>();

    private readonly IDeleteBusinessStatusUseCase _deleteBusinessStatusUseCase =
        Substitute.For<IDeleteBusinessStatusUseCase>();

    private BusinessStatusesController Sut =>
        new(_createBusinessStatusUseCase,
            _getBusinessStatusByIdUseCase,
            _updateBusinessStatusUseCase,
            _deleteBusinessStatusUseCase);

    private static async Task<(int StatusCode, string Body)> RunAsync(IActionResult result)
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

    private static async Task<(int StatusCode, JsonDocument Body)> ExecuteAsync(IActionResult result)
    {
        var (statusCode, body) = await RunAsync(result).ConfigureAwait(false);
        return (statusCode, JsonDocument.Parse(body));
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

    // ── GET /business-statuses/{id} ───────────────────────────────────────────

    [Fact]
    public async Task GetBusinessStatusById_WhenTheStatusExists_ReturnsOkWithTheResource()
    {
        _getBusinessStatusByIdUseCase.ExecuteAsync(7, Arg.Any<CancellationToken>())
            .Returns(Result<GetBusinessStatusByIdOutputDto>.Success(
                new GetBusinessStatusByIdOutputDto(7, "Negotiation", 50, "49ff7c", true)));

        var result = await Sut.GetBusinessStatusById(7, CancellationToken.None);
        var (statusCode, body) = await ExecuteAsync(result);

        statusCode.ShouldBe(StatusCodes.Status200OK);
        var data = body.RootElement.GetProperty("data");
        data.GetProperty("id").GetInt32().ShouldBe(7);
        data.GetProperty("name").GetString().ShouldBe("Negotiation");
        data.GetProperty("percentage").GetInt32().ShouldBe(50);
        data.GetProperty("color").GetString().ShouldBe("49ff7c");
        data.GetProperty("isActive").GetBoolean().ShouldBeTrue();
        await _getBusinessStatusByIdUseCase.Received(1).ExecuteAsync(7, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetBusinessStatusById_WithAnUnknownId_ReturnsNotFound()
    {
        _getBusinessStatusByIdUseCase.ExecuteAsync(999, Arg.Any<CancellationToken>())
            .Returns(Result<GetBusinessStatusByIdOutputDto>.Failure(
                BusinessStatusErrors.NotFound(999) with { Context = BusinessStatusErrors.Context }));

        var result = await Sut.GetBusinessStatusById(999, CancellationToken.None);
        var (statusCode, body) = await ExecuteAsync(result);

        statusCode.ShouldBe(StatusCodes.Status404NotFound);
        body.RootElement.GetProperty("error").GetProperty("type").GetString().ShouldBe("NOT_FOUND");
    }

    [Fact]
    public async Task GetBusinessStatusById_WhenTheQueryFails_ReturnsInternalServerError()
    {
        _getBusinessStatusByIdUseCase.ExecuteAsync(7, Arg.Any<CancellationToken>())
            .Returns(Result<GetBusinessStatusByIdOutputDto>.Failure(
                new DomainError("boom", ErrorType.Internal)));

        var result = await Sut.GetBusinessStatusById(7, CancellationToken.None);
        var (statusCode, _) = await ExecuteAsync(result);

        statusCode.ShouldBe(StatusCodes.Status500InternalServerError);
    }

    // ── PUT /business-statuses/{id} ───────────────────────────────────────────

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

    // ── DELETE /business-statuses/{id} ────────────────────────────────────────

    [Fact]
    public async Task DeleteBusinessStatus_WhenTheUseCaseSucceeds_ReturnsNoContentWithoutBody()
    {
        _deleteBusinessStatusUseCase.ExecuteAsync(Id, Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        var result = await Sut.DeleteBusinessStatus(Id, CancellationToken.None);
        var (statusCode, body) = await RunAsync(result);

        statusCode.ShouldBe(StatusCodes.Status204NoContent);
        body.ShouldBeEmpty();
        await _deleteBusinessStatusUseCase.Received(1).ExecuteAsync(Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteBusinessStatus_WhenTheStatusDoesNotExist_ReturnsNotFound()
    {
        _deleteBusinessStatusUseCase.ExecuteAsync(Id, Arg.Any<CancellationToken>())
            .Returns(Result.Failure(BusinessStatusErrors.NotFound(Id)));

        var result = await Sut.DeleteBusinessStatus(Id, CancellationToken.None);
        var (statusCode, _) = await ExecuteAsync(result);

        statusCode.ShouldBe(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task DeleteBusinessStatus_WhenTheStatusIsTerminal_ReturnsConflict()
    {
        _deleteBusinessStatusUseCase.ExecuteAsync(Id, Arg.Any<CancellationToken>())
            .Returns(Result.Failure(BusinessStatusErrors.TerminalCannotBeDeleted));

        var result = await Sut.DeleteBusinessStatus(Id, CancellationToken.None);
        var (statusCode, _) = await ExecuteAsync(result);

        statusCode.ShouldBe(StatusCodes.Status409Conflict);
    }

    [Fact]
    public async Task DeleteBusinessStatus_WhenTheStatusIsInUse_ReturnsConflict()
    {
        _deleteBusinessStatusUseCase.ExecuteAsync(Id, Arg.Any<CancellationToken>())
            .Returns(Result.Failure(BusinessStatusErrors.StatusInUse(Id)));

        var result = await Sut.DeleteBusinessStatus(Id, CancellationToken.None);
        var (statusCode, _) = await ExecuteAsync(result);

        statusCode.ShouldBe(StatusCodes.Status409Conflict);
    }

    [Fact]
    public async Task DeleteBusinessStatus_WhenPersistenceFails_ReturnsInternalServerError()
    {
        _deleteBusinessStatusUseCase.ExecuteAsync(Id, Arg.Any<CancellationToken>())
            .Returns(Result.Failure(new DomainError("boom", ErrorType.Internal)));

        var result = await Sut.DeleteBusinessStatus(Id, CancellationToken.None);
        var (statusCode, _) = await ExecuteAsync(result);

        statusCode.ShouldBe(StatusCodes.Status500InternalServerError);
    }
}
