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

public sealed class HttpOkPagedResultTests
{
    private sealed record SampleItem(int Id);

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
    public async Task ExecuteResultAsync_WhenResultIsSuccess_Writes200WithItemsAndTotalCount()
    {
        var items = new List<SampleItem> { new(1), new(2) };
        var result = PagedResult<SampleItem>.Success(items, totalCount: 5);
        var httpContext = CreateHttpContext();
        var sut = new HttpOkPagedResult<SampleItem>(result);

        await sut.ExecuteResultAsync(CreateActionContext(httpContext));

        httpContext.Response.StatusCode.ShouldBe(StatusCodes.Status200OK);
        using var doc = await ReadBodyAsync(httpContext);
        doc.RootElement.GetProperty("data").GetProperty("totalCount").GetInt32().ShouldBe(5);
        doc.RootElement.GetProperty("data").GetProperty("items").GetArrayLength().ShouldBe(2);
    }

    [Fact]
    public async Task ExecuteResultAsync_WhenResultIsFailure_DelegatesToErrorResponse()
    {
        var result = PagedResult<SampleItem>.Failure(new NotFoundError("None found."));
        var httpContext = CreateHttpContext();
        var sut = new HttpOkPagedResult<SampleItem>(result);

        await sut.ExecuteResultAsync(CreateActionContext(httpContext));

        httpContext.Response.StatusCode.ShouldBe(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task ImplicitOperator_FromDomainError_CreatesFailurePagedResult()
    {
        PagedResult<SampleItem> result = new NotFoundError("None found.");
        var httpContext = CreateHttpContext();
        var sut = new HttpOkPagedResult<SampleItem>(result);

        await sut.ExecuteResultAsync(CreateActionContext(httpContext));

        httpContext.Response.StatusCode.ShouldBe(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task ImplicitOperator_FromSuccessPagedResult_CreatesHttpOkPagedResultThatWrites200()
    {
        var result = PagedResult<SampleItem>.Success([new SampleItem(1)], totalCount: 1);
        HttpOkPagedResult<SampleItem> sut = result;
        var httpContext = CreateHttpContext();

        await sut.ExecuteResultAsync(CreateActionContext(httpContext));

        httpContext.Response.StatusCode.ShouldBe(StatusCodes.Status200OK);
    }
}
