using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Routing;
using Shared.Presentation.Results;
using Shared.Results;
using Shared.Results.Errors;
using Shouldly;
using Xunit;

namespace UnitTests.Shared.Presentation;

public sealed class HttpResultTests
{
    private sealed record SampleDto(string Name);

    private static DefaultHttpContext CreateHttpContext()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static ActionContext CreateActionContext(HttpContext httpContext) =>
        new(httpContext, new RouteData(), new ActionDescriptor());

    private static async Task<JsonDocument> ReadBodyAsync(HttpContext context)
    {
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body, leaveOpen: true);
        var json = await reader.ReadToEndAsync().ConfigureAwait(false);
        return JsonDocument.Parse(json);
    }

    [Fact]
    public async Task ExecuteResultAsync_HttpOkResultOnSuccess_Writes200WithData()
    {
        var result = Result<SampleDto>.Success(new SampleDto("Alice"));
        var httpContext = CreateHttpContext();
        var sut = new HttpOkResult<SampleDto>(result);

        await sut.ExecuteResultAsync(CreateActionContext(httpContext));

        httpContext.Response.StatusCode.ShouldBe(StatusCodes.Status200OK);
        using var doc = await ReadBodyAsync(httpContext);
        doc.RootElement.GetProperty("statusCode").GetInt32().ShouldBe(200);
        doc.RootElement.GetProperty("data").GetProperty("name").GetString().ShouldBe("Alice");
    }

    [Fact]
    public async Task ExecuteResultAsync_HttpOkResultOnFailure_DelegatesToErrorResponse()
    {
        Result<SampleDto> result = new NotFoundError("Not found.");
        var httpContext = CreateHttpContext();
        var sut = new HttpOkResult<SampleDto>(result);

        await sut.ExecuteResultAsync(CreateActionContext(httpContext));

        httpContext.Response.StatusCode.ShouldBe(StatusCodes.Status404NotFound);
        using var doc = await ReadBodyAsync(httpContext);
        doc.RootElement.GetProperty("error").GetProperty("type").GetString().ShouldBe("NOT_FOUND");
    }

    [Fact]
    public async Task ExecuteResultAsync_HttpCreatedResultOnSuccess_Writes201WithData()
    {
        var result = Result<SampleDto>.Success(new SampleDto("Bob"));
        var httpContext = CreateHttpContext();
        var sut = new HttpCreatedResult<SampleDto>(result);

        await sut.ExecuteResultAsync(CreateActionContext(httpContext));

        httpContext.Response.StatusCode.ShouldBe(StatusCodes.Status201Created);
        using var doc = await ReadBodyAsync(httpContext);
        doc.RootElement.GetProperty("statusCode").GetInt32().ShouldBe(201);
    }

    [Fact]
    public async Task ExecuteResultAsync_HttpCreatedResultOnFailure_DelegatesToErrorResponse()
    {
        Result<SampleDto> result = new ValidationError("Invalid.", ErrorType.Validation) { Property = "name" };
        var httpContext = CreateHttpContext();
        var sut = new HttpCreatedResult<SampleDto>(result);

        await sut.ExecuteResultAsync(CreateActionContext(httpContext));

        httpContext.Response.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task ImplicitOperator_FromSuccessResult_CreatesHttpOkResultThatWrites200()
    {
        Result<SampleDto> result = Result<SampleDto>.Success(new SampleDto("Carl"));
        HttpOkResult<SampleDto> sut = result;
        var httpContext = CreateHttpContext();

        await sut.ExecuteResultAsync(CreateActionContext(httpContext));

        httpContext.Response.StatusCode.ShouldBe(StatusCodes.Status200OK);
    }

    [Fact]
    public async Task ImplicitOperator_FromSuccessResult_CreatesHttpCreatedResultThatWrites201()
    {
        Result<SampleDto> result = Result<SampleDto>.Success(new SampleDto("Dana"));
        HttpCreatedResult<SampleDto> sut = result;
        var httpContext = CreateHttpContext();

        await sut.ExecuteResultAsync(CreateActionContext(httpContext));

        httpContext.Response.StatusCode.ShouldBe(StatusCodes.Status201Created);
    }
}
