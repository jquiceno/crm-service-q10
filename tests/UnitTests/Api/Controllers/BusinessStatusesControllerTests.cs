using System.Text.Json;
using Api.Controllers;
using BusinessStatus.Application.UseCases.CreateBusinessStatus;
using BusinessStatus.Domain.Errors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Routing;
using NSubstitute;
using Shared.Presentation.Results;
using Shared.Results;
using Shared.Results.Errors;
using Shouldly;
using Xunit;

namespace UnitTests.Api.Controllers;

public sealed class BusinessStatusesControllerTests
{
    private readonly ICreateBusinessStatusUseCase _createBusinessStatusUseCase =
        Substitute.For<ICreateBusinessStatusUseCase>();

    private BusinessStatusesController Sut => new(_createBusinessStatusUseCase);

    private static async Task<(int StatusCode, JsonDocument Body)> ExecuteAsync(
        HttpCreatedResult<CreateBusinessStatusOutputDto> result)
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
}
