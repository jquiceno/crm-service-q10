using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Routing;
using Shared.Presentation.Filters;
using Shared.Results.Errors;
using Shouldly;
using Xunit;

namespace UnitTests.Shared.Presentation;

public sealed class ValidationErrorResultTests
{
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
    public async Task ExecuteResultAsync_WithValidationError_WritesBadRequestWithErrorDetails()
    {
        var error = new ValidationError("Name is required.", ErrorType.Validation) { Property = "Name" };
        var httpContext = CreateHttpContext();
        var sut = new ValidationErrorResult(error);

        await sut.ExecuteResultAsync(CreateActionContext(httpContext));

        httpContext.Response.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
        using var doc = await ReadBodyAsync(httpContext);
        doc.RootElement.GetProperty("statusCode").GetInt32().ShouldBe(400);
        doc.RootElement.GetProperty("error").GetProperty("type").GetString().ShouldBe("VALIDATION");
        doc.RootElement.GetProperty("error").GetProperty("code").GetString().ShouldBe("HTTP.VALIDATION");
        doc.RootElement.GetProperty("error").GetProperty("message").GetString().ShouldBe("Name is required.");
    }

    [Fact]
    public async Task ExecuteResultAsync_WithNotFoundError_WritesNotFoundStatusCode()
    {
        var error = new NotFoundError("Announcement not found.");
        var httpContext = CreateHttpContext();
        var sut = new ValidationErrorResult(error);

        await sut.ExecuteResultAsync(CreateActionContext(httpContext));

        httpContext.Response.StatusCode.ShouldBe(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task ExecuteResultAsync_WithErrorDetails_SerializesDetailsArray()
    {
        var detail = new ErrorDetail("Email", ["Invalid format."]);
        var error = new DomainError("Validation failed.", ErrorType.Validation) { Details = [detail] };
        var httpContext = CreateHttpContext();
        var sut = new ValidationErrorResult(error);

        await sut.ExecuteResultAsync(CreateActionContext(httpContext));

        using var doc = await ReadBodyAsync(httpContext);
        var details = doc.RootElement.GetProperty("error").GetProperty("details");
        details.GetArrayLength().ShouldBe(1);
        details[0].GetProperty("property").GetString().ShouldBe("email");
    }
}
