using System.Text.Json;
using Api.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Routing;
using NSubstitute;
using ServiceInfo.Application.Ports;
using ServiceInfo.Application.UseCases.GetServiceInfo;
using Shared.Presentation.Results;
using Shared.Results;
using Shared.Results.Errors;
using Shouldly;
using Xunit;

namespace UnitTests.Api.Controllers;

public sealed class ServiceInfoControllerTests
{
    private readonly IGetServiceInfoUseCase _getServiceInfoPort = Substitute.For<IGetServiceInfoUseCase>();

    private static async Task<(int StatusCode, JsonDocument Body)> ExecuteAsync(
        HttpOkResult<GetServiceInfoOutputDto> result)
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
    public async Task GetInfo_WhenUseCaseSucceeds_ReturnsOkWithServiceInfo()
    {
        var output = new GetServiceInfoOutputDto("ok", "service-template-dotnet", "1.0.0", "2.0.0");
        _getServiceInfoPort.ExecuteAsync(Arg.Any<CancellationToken>())
            .Returns(Result<GetServiceInfoOutputDto>.Success(output));

        var result = await new ServiceInfoController().GetInfo(_getServiceInfoPort, CancellationToken.None);
        var (statusCode, body) = await ExecuteAsync(result);

        statusCode.ShouldBe(StatusCodes.Status200OK);
        body.RootElement.GetProperty("data").GetProperty("name").GetString().ShouldBe("service-template-dotnet");
        body.RootElement.GetProperty("data").GetProperty("status").GetString().ShouldBe("ok");
        await _getServiceInfoPort.Received(1).ExecuteAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetInfo_WhenUseCaseFails_ReturnsMappedErrorStatus()
    {
        _getServiceInfoPort.ExecuteAsync(Arg.Any<CancellationToken>())
            .Returns(Result<GetServiceInfoOutputDto>.Failure(new DomainError("boom", ErrorType.Internal)));

        var result = await new ServiceInfoController().GetInfo(_getServiceInfoPort, CancellationToken.None);
        var (statusCode, _) = await ExecuteAsync(result);

        statusCode.ShouldBe(StatusCodes.Status500InternalServerError);
    }
}
