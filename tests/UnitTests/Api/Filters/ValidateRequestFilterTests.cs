using System.Text;
using System.Text.Json;
using Api.Filters;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using NSubstitute;
using Shared.Application.Ports;
using Shared.Presentation.Attributes;
using Shared.Presentation.Filters;
using Shared.Results;
using Shared.Results.Errors;
using Shouldly;
using Xunit;

namespace UnitTests.Api.Filters;

public sealed class ValidateRequestFilterTests
{
    public sealed record TestDto(int Age, string? Name);

    private sealed class NonSeekableStream(Stream inner) : Stream
    {
        public override bool CanRead => inner.CanRead;
        public override bool CanSeek => false;
        public override bool CanWrite => inner.CanWrite;
        public override long Length => inner.Length;

        public override long Position
        {
            get => inner.Position;
            set => throw new NotSupportedException();
        }

        public override void Flush() => inner.Flush();
        public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => inner.Write(buffer, offset, count);
    }

    private readonly ValidateRequestFilter _sut = new();

    private static MemoryStream JsonBody(string json) => new(Encoding.UTF8.GetBytes(json));

    private static DefaultHttpContext BuildHttpContext(Stream? body, IServiceProvider services)
    {
        var context = new DefaultHttpContext { RequestServices = services };
        if (body is not null)
            context.Request.Body = body;
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static ActionDescriptor BuildActionDescriptor(
        IList<ParameterDescriptor>? parameters = null,
        IList<object>? endpointMetadata = null) =>
        new()
        {
            Parameters = parameters ?? new List<ParameterDescriptor>(),
            EndpointMetadata = endpointMetadata ?? [],
        };

    private static ParameterDescriptor BodyParameter(Type type) =>
        new()
        {
            Name = "input",
            ParameterType = type,
            BindingInfo = new BindingInfo { BindingSource = BindingSource.Body },
        };

    private static (ActionExecutingContext Context, bool[] NextCalled) BuildExecutingContext(
        HttpContext httpContext,
        ActionDescriptor actionDescriptor,
        IDictionary<string, object?> actionArguments,
        ModelStateDictionary? modelState = null)
    {
        var actionContext = new ActionContext(
            httpContext, new RouteData(), actionDescriptor, modelState ?? new ModelStateDictionary());
        var context = new ActionExecutingContext(
            actionContext, new List<IFilterMetadata>(), actionArguments, controller: new object());
        return (context, [false]);
    }

    private static ActionExecutionDelegate BuildNext(ActionExecutingContext context, bool[] nextCalled) =>
        () =>
        {
            nextCalled[0] = true;
            return Task.FromResult(new ActionExecutedContext(context, new List<IFilterMetadata>(), context.Controller));
        };

    // Executes the real IActionResult against the HttpContext and reads the serialized
    // error.details array — avoids reflecting into ValidationErrorResult's private state.
    private static async Task<JsonElement> ExecuteResultAndGetDetailsAsync(HttpContext httpContext, IActionResult result)
    {
        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        await result.ExecuteResultAsync(actionContext).ConfigureAwait(false);
        httpContext.Response.Body.Position = 0;
        using var reader = new StreamReader(httpContext.Response.Body, leaveOpen: true);
        var json = await reader.ReadToEndAsync().ConfigureAwait(false);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("error").GetProperty("details").Clone();
    }

    [Fact]
    public async Task OnActionExecutionAsync_WhenModelStateInvalidWithoutJsonErrors_SetsValidationErrorResultAndSkipsNext()
    {
        var modelState = new ModelStateDictionary();
        modelState.AddModelError("name", "Name is required.");
        var httpContext = BuildHttpContext(body: null, Substitute.For<IServiceProvider>());
        var (context, nextCalled) = BuildExecutingContext(
            httpContext, BuildActionDescriptor(), new Dictionary<string, object?>(), modelState);

        await _sut.OnActionExecutionAsync(context, BuildNext(context, nextCalled));

        context.Result.ShouldBeOfType<ValidationErrorResult>();
        nextCalled[0].ShouldBeFalse();
    }

    [Fact]
    public async Task OnActionExecutionAsync_WhenJsonErrorsButBodyNotSeekable_FallsBackToModelStateErrors()
    {
        var modelState = new ModelStateDictionary();
        modelState.AddModelError("$.name", "invalid");
        var body = new NonSeekableStream(JsonBody("{}"));
        var httpContext = BuildHttpContext(body, Substitute.For<IServiceProvider>());
        var descriptor = BuildActionDescriptor(parameters: [BodyParameter(typeof(TestDto))]);
        var (context, nextCalled) = BuildExecutingContext(
            httpContext, descriptor, new Dictionary<string, object?>(), modelState);

        await _sut.OnActionExecutionAsync(context, BuildNext(context, nextCalled));

        context.Result.ShouldBeOfType<ValidationErrorResult>();
        nextCalled[0].ShouldBeFalse();
    }

    [Fact]
    public async Task OnActionExecutionAsync_WhenJsonErrorsButNoBodyParameter_FallsBackToModelStateErrors()
    {
        var modelState = new ModelStateDictionary();
        modelState.AddModelError("$.name", "invalid");
        var httpContext = BuildHttpContext(body: null, Substitute.For<IServiceProvider>());
        var (context, nextCalled) = BuildExecutingContext(
            httpContext, BuildActionDescriptor(), new Dictionary<string, object?>(), modelState);

        await _sut.OnActionExecutionAsync(context, BuildNext(context, nextCalled));

        context.Result.ShouldBeOfType<ValidationErrorResult>();
        nextCalled[0].ShouldBeFalse();
    }

    [Fact]
    public async Task OnActionExecutionAsync_WhenJsonBodyHasTypeMismatch_SetsValidationErrorResultFromScannedErrors()
    {
        var modelState = new ModelStateDictionary();
        modelState.AddModelError("$.age", "invalid");
        var body = JsonBody("""{"age":"not-a-number","name":"ok"}""");
        var httpContext = BuildHttpContext(body, Substitute.For<IServiceProvider>());
        var descriptor = BuildActionDescriptor(parameters: [BodyParameter(typeof(TestDto))]);
        var (context, nextCalled) = BuildExecutingContext(
            httpContext, descriptor, new Dictionary<string, object?>(), modelState);

        await _sut.OnActionExecutionAsync(context, BuildNext(context, nextCalled));

        context.Result.ShouldBeOfType<ValidationErrorResult>();
        var details = await ExecuteResultAndGetDetailsAsync(httpContext, context.Result!);
        details.EnumerateArray().ShouldContain(d => d.GetProperty("property").GetString() == "age");
        nextCalled[0].ShouldBeFalse();
    }

    [Fact]
    public async Task OnActionExecutionAsync_WhenJsonBodyTypesMatch_FallsBackToModelStateErrors()
    {
        var modelState = new ModelStateDictionary();
        modelState.AddModelError("$.age", "invalid");
        var body = JsonBody("""{"age":5,"name":"ok"}""");
        var httpContext = BuildHttpContext(body, Substitute.For<IServiceProvider>());
        var descriptor = BuildActionDescriptor(parameters: [BodyParameter(typeof(TestDto))]);
        var (context, nextCalled) = BuildExecutingContext(
            httpContext, descriptor, new Dictionary<string, object?>(), modelState);

        await _sut.OnActionExecutionAsync(context, BuildNext(context, nextCalled));

        context.Result.ShouldBeOfType<ValidationErrorResult>();
        // Falls back to the ModelState-derived message ("Invalid JSON format.") since the
        // scanned JSON types actually matched — proves the scanned.Count == 0 branch.
        var details = await ExecuteResultAndGetDetailsAsync(httpContext, context.Result!);
        details.EnumerateArray()
            .Any(d => d.TryGetProperty("errors", out var errors) &&
                errors.EnumerateArray().Any(e => e.GetString() == "Invalid JSON format."))
            .ShouldBeTrue();
        nextCalled[0].ShouldBeFalse();
    }

    [Fact]
    public async Task OnActionExecutionAsync_WhenJsonBodyMalformed_CatchesExceptionAndFallsBackToModelStateErrors()
    {
        var modelState = new ModelStateDictionary();
        modelState.AddModelError("$.age", "invalid");
        var body = JsonBody("{not-valid-json");
        var httpContext = BuildHttpContext(body, Substitute.For<IServiceProvider>());
        var descriptor = BuildActionDescriptor(parameters: [BodyParameter(typeof(TestDto))]);
        var (context, nextCalled) = BuildExecutingContext(
            httpContext, descriptor, new Dictionary<string, object?>(), modelState);

        await _sut.OnActionExecutionAsync(context, BuildNext(context, nextCalled));

        context.Result.ShouldBeOfType<ValidationErrorResult>();
        nextCalled[0].ShouldBeFalse();
    }

    [Fact]
    public async Task OnActionExecutionAsync_WhenModelStateValidAndNoValidateRequestAttribute_CallsNextWithoutValidation()
    {
        var services = Substitute.For<IServiceProvider>();
        var httpContext = BuildHttpContext(body: null, services);
        var (context, nextCalled) = BuildExecutingContext(
            httpContext, BuildActionDescriptor(), new Dictionary<string, object?>());

        await _sut.OnActionExecutionAsync(context, BuildNext(context, nextCalled));

        nextCalled[0].ShouldBeTrue();
        context.Result.ShouldBeNull();
        services.DidNotReceiveWithAnyArgs().GetService(default!);
    }

    [Fact]
    public async Task OnActionExecutionAsync_WhenAllArgumentsAreSimpleOrNull_SkipsValidatorLookupAndCallsNext()
    {
        var services = Substitute.For<IServiceProvider>();
        var httpContext = BuildHttpContext(body: null, services);
        var descriptor = BuildActionDescriptor(endpointMetadata: [new ValidateRequestAttribute()]);
        var actionArguments = new Dictionary<string, object?>
        {
            ["id"] = 5,
            ["code"] = "ABC",
            ["key"] = Guid.NewGuid(),
            ["when"] = DateTime.UtcNow,
            ["opt"] = null,
        };
        var (context, nextCalled) = BuildExecutingContext(httpContext, descriptor, actionArguments);

        await _sut.OnActionExecutionAsync(context, BuildNext(context, nextCalled));

        nextCalled[0].ShouldBeTrue();
        context.Result.ShouldBeNull();
        services.DidNotReceiveWithAnyArgs().GetService(default!);
    }

    [Fact]
    public async Task OnActionExecutionAsync_WhenComplexArgumentHasNoRegisteredValidator_SkipsAndCallsNext()
    {
        var services = Substitute.For<IServiceProvider>();
        services.GetService(typeof(IRequestValidatorPort<TestDto>)).Returns((object?)null);
        var httpContext = BuildHttpContext(body: null, services);
        var descriptor = BuildActionDescriptor(endpointMetadata: [new ValidateRequestAttribute()]);
        var actionArguments = new Dictionary<string, object?> { ["input"] = new TestDto(1, "ok") };
        var (context, nextCalled) = BuildExecutingContext(httpContext, descriptor, actionArguments);

        await _sut.OnActionExecutionAsync(context, BuildNext(context, nextCalled));

        nextCalled[0].ShouldBeTrue();
        context.Result.ShouldBeNull();
    }

    [Fact]
    public async Task OnActionExecutionAsync_WhenRegisteredValidatorSucceeds_CallsNext()
    {
        var validator = Substitute.For<IRequestValidatorPort<TestDto>>();
        ((IRequestValidatorPort)validator)
            .ValidateAsync(Arg.Any<object>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Success()));
        var services = Substitute.For<IServiceProvider>();
        services.GetService(typeof(IRequestValidatorPort<TestDto>)).Returns(validator);
        var httpContext = BuildHttpContext(body: null, services);
        var descriptor = BuildActionDescriptor(endpointMetadata: [new ValidateRequestAttribute()]);
        var actionArguments = new Dictionary<string, object?> { ["input"] = new TestDto(1, "ok") };
        var (context, nextCalled) = BuildExecutingContext(httpContext, descriptor, actionArguments);

        await _sut.OnActionExecutionAsync(context, BuildNext(context, nextCalled));

        nextCalled[0].ShouldBeTrue();
        context.Result.ShouldBeNull();
    }

    [Fact]
    public async Task OnActionExecutionAsync_WhenRegisteredValidatorFails_SetsValidationErrorResultAndSkipsNext()
    {
        var failure = new ValidationError("Age is invalid.", ErrorType.Validation) { Property = "Age" };
        var validator = Substitute.For<IRequestValidatorPort<TestDto>>();
        ((IRequestValidatorPort)validator)
            .ValidateAsync(Arg.Any<object>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Result>(failure));
        var services = Substitute.For<IServiceProvider>();
        services.GetService(typeof(IRequestValidatorPort<TestDto>)).Returns(validator);
        var httpContext = BuildHttpContext(body: null, services);
        var descriptor = BuildActionDescriptor(endpointMetadata: [new ValidateRequestAttribute()]);
        var actionArguments = new Dictionary<string, object?> { ["input"] = new TestDto(1, "ok") };
        var (context, nextCalled) = BuildExecutingContext(httpContext, descriptor, actionArguments);

        await _sut.OnActionExecutionAsync(context, BuildNext(context, nextCalled));

        context.Result.ShouldBeOfType<ValidationErrorResult>();
        nextCalled[0].ShouldBeFalse();
    }
}
