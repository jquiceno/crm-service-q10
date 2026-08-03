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

public sealed class HttpNoContentResultTests
{
    private static DefaultHttpContext CreateHttpContext()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static ActionContext CreateActionContext(HttpContext httpContext) =>
        new(httpContext, new RouteData(), new ActionDescriptor());

    [Fact]
    public async Task ExecuteResultAsync_WhenResultIsSuccess_Writes204WithEmptyBody()
    {
        var httpContext = CreateHttpContext();
        var sut = new HttpNoContentResult(Result.Success());

        await sut.ExecuteResultAsync(CreateActionContext(httpContext));

        httpContext.Response.StatusCode.ShouldBe(StatusCodes.Status204NoContent);
        httpContext.Response.Body.Length.ShouldBe(0);
    }

    [Fact]
    public async Task ExecuteResultAsync_WhenResultIsFailure_DelegatesToErrorResponse()
    {
        var httpContext = CreateHttpContext();
        var sut = new HttpNoContentResult(new ConflictError("Already exists."));

        await sut.ExecuteResultAsync(CreateActionContext(httpContext));

        httpContext.Response.StatusCode.ShouldBe(StatusCodes.Status409Conflict);
    }

    [Fact]
    public async Task ImplicitOperator_FromSuccessResult_CreatesHttpNoContentResultThatWrites204()
    {
        var httpContext = CreateHttpContext();
        HttpNoContentResult sut = Result.Success();

        await sut.ExecuteResultAsync(CreateActionContext(httpContext));

        httpContext.Response.StatusCode.ShouldBe(StatusCodes.Status204NoContent);
    }
}
