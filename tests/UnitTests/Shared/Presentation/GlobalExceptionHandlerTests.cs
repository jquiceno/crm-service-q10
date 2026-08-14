using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Logging.Abstractions;
using Shared.Presentation.Middleware;
using Shouldly;
using Xunit;

namespace UnitTests.Shared.Presentation;

public sealed class GlobalExceptionHandlerTests
{
    private readonly GlobalExceptionHandler _sut = new(NullLogger<GlobalExceptionHandler>.Instance);

    private sealed class StartedHttpResponseFeature : IHttpResponseFeature
    {
        public int StatusCode { get; set; } = StatusCodes.Status200OK;
        public string? ReasonPhrase { get; set; }
        public IHeaderDictionary Headers { get; set; } = new HeaderDictionary();
        public Stream Body { get; set; } = new MemoryStream();
        public bool HasStarted => true;
        public void OnStarting(Func<object, Task> callback, object state) { }
        public void OnCompleted(Func<object, Task> callback, object state) { }
    }

    private static DefaultHttpContext CreateContext()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        return context;
    }

    [Fact]
    public async Task TryHandleAsync_WhenOperationCanceledAndRequestAborted_Returns499AndHandlesTrue()
    {
        var context = CreateContext();
        context.RequestAborted = new CancellationToken(canceled: true);

        var handled = await _sut.TryHandleAsync(context, new OperationCanceledException(), CancellationToken.None);

        handled.ShouldBeTrue();
        context.Response.StatusCode.ShouldBe(499);
    }

    [Fact]
    public async Task TryHandleAsync_WhenOperationCanceledButRequestNotAborted_FallsThroughToInternalServerError()
    {
        var context = CreateContext();

        var handled = await _sut.TryHandleAsync(context, new OperationCanceledException(), CancellationToken.None);

        handled.ShouldBeTrue();
        context.Response.StatusCode.ShouldBe(StatusCodes.Status500InternalServerError);
    }

    [Fact]
    public async Task TryHandleAsync_WhenRequestAbortedButExceptionIsNotOperationCanceled_WritesInternalServerError()
    {
        var context = CreateContext();
        context.RequestAborted = new CancellationToken(canceled: true);

        var handled = await _sut.TryHandleAsync(context, new InvalidOperationException("boom"), CancellationToken.None);

        handled.ShouldBeTrue();
        context.Response.StatusCode.ShouldBe(StatusCodes.Status500InternalServerError);
    }

    [Fact]
    public async Task TryHandleAsync_WhenResponseNotStarted_Writes500WithInternalErrorBody()
    {
        var context = CreateContext();

        var handled = await _sut.TryHandleAsync(context, new InvalidOperationException("boom"), CancellationToken.None);

        handled.ShouldBeTrue();
        context.Response.StatusCode.ShouldBe(StatusCodes.Status500InternalServerError);
        context.Response.ContentType.ShouldStartWith("application/json");

        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        var json = await reader.ReadToEndAsync();
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("statusCode").GetInt32().ShouldBe(500);
        doc.RootElement.GetProperty("error").GetProperty("type").GetString().ShouldBe("INTERNAL");
        doc.RootElement.GetProperty("error").GetProperty("code").GetString().ShouldBe("HTTP.INTERNAL");
        doc.RootElement.GetProperty("error").GetProperty("message").GetString().ShouldBe("An unexpected error occurred.");
        doc.RootElement.GetProperty("error").GetProperty("details").GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task TryHandleAsync_WhenResponseAlreadyStarted_ReturnsFalseAndDoesNotWriteBody()
    {
        var context = new DefaultHttpContext();
        context.Features.Set<IHttpResponseFeature>(new StartedHttpResponseFeature());

        var handled = await _sut.TryHandleAsync(context, new InvalidOperationException("boom"), CancellationToken.None);

        handled.ShouldBeFalse();
        context.Response.StatusCode.ShouldBe(StatusCodes.Status200OK);
        context.Response.ContentType.ShouldBeNull();
    }
}
