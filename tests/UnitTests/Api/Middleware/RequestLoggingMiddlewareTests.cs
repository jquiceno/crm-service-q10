using System.Diagnostics;
using Api.Middleware;
using Infrastructure.Adapters.Logging;
using Microsoft.AspNetCore.Http;
using NSubstitute;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Shared.Application.Ports;
using Shouldly;
using Xunit;

namespace UnitTests.Api.Middleware;

public sealed class RequestLoggingMiddlewareTests
{
    private sealed class CollectingSink : ILogEventSink
    {
        public List<LogEvent> Events { get; } = [];
        public void Emit(LogEvent logEvent) => Events.Add(logEvent);
    }

    private readonly ILoggerPort<RequestLoggingMiddleware> _logger = Substitute.For<ILoggerPort<RequestLoggingMiddleware>>();

    private static DefaultHttpContext BuildContext(string method = "GET", string path = "/api/v1/products")
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();
        return context;
    }

    [Fact]
    public async Task InvokeAsync_OnSuccess_CallsNextAndLogsCompletion()
    {
        var nextCalled = false;
        var sut = new RequestLoggingMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        }, _logger);
        var context = BuildContext();

        await sut.InvokeAsync(context);

        nextCalled.ShouldBeTrue();
        _logger.Received(1).Info("http.request.completed");
    }

    [Fact]
    public async Task InvokeAsync_WhenNextThrows_StillLogsCompletionAndRethrows()
    {
        var sut = new RequestLoggingMiddleware(_ => throw new InvalidOperationException("boom"), _logger);
        var context = BuildContext();

        await Should.ThrowAsync<InvalidOperationException>(() => sut.InvokeAsync(context));

        _logger.Received(1).Info("http.request.completed");
    }

    [Fact]
    public async Task InvokeAsync_WhenActivityIsCurrent_RegistersOnStartingCallbackWithoutThrowing()
    {
        using var listener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
        };
        ActivitySource.AddActivityListener(listener);
        using var source = new ActivitySource(nameof(InvokeAsync_WhenActivityIsCurrent_RegistersOnStartingCallbackWithoutThrowing));
        using var activity = source.StartActivity("test-activity");
        activity.ShouldNotBeNull();

        var sut = new RequestLoggingMiddleware(_ => Task.CompletedTask, _logger);
        var context = BuildContext();

        await sut.InvokeAsync(context);

        // The traceId is not null while an Activity is current, so InvokeAsync registers an
        // OnStarting callback. DefaultHttpContext has no public API to fire lifecycle
        // callbacks outside a real server, so the header write inside that callback is not
        // independently observable here — only that registration succeeds without throwing.
        _logger.Received(1).Info("http.request.completed");
    }

    [Fact]
    public async Task InvokeAsync_WhenNoActivityIsCurrent_SkipsTraceHeaderRegistration()
    {
        Activity.Current = null;
        var sut = new RequestLoggingMiddleware(_ => Task.CompletedTask, _logger);
        var context = BuildContext();

        await sut.InvokeAsync(context);

        _logger.Received(1).Info("http.request.completed");
    }

    private static (ILoggerPort<RequestLoggingMiddleware> Logger, CollectingSink Sink) BuildRealLogger()
    {
        var sink = new CollectingSink();
        var serilogLogger = new LoggerConfiguration()
            .Enrich.FromLogContext()
            .MinimumLevel.Verbose()
            .WriteTo.Sink(sink)
            .CreateLogger();
        return (new SerilogLoggerAdapter<RequestLoggingMiddleware>(serilogLogger), sink);
    }

    [Fact]
    public async Task InvokeAsync_WithRemoteAddressAndPath_BuildsHttpPropertiesFromRequest()
    {
        var (logger, sink) = BuildRealLogger();
        var sut = new RequestLoggingMiddleware(_ => Task.CompletedTask, logger);
        var context = BuildContext(method: "POST", path: "/api/v1/products");
        context.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("10.0.0.5");
        context.Request.Headers.UserAgent = "test-agent";
        context.Response.StatusCode = 201;

        await sut.InvokeAsync(context);

        sink.Events.Count.ShouldBe(1);
        var http = sink.Events[0].Properties["http"] as StructureValue;
        http.ShouldNotBeNull();
        http!.Properties.Single(p => p.Name == "Method").Value.ToString().ShouldContain("POST");
        http.Properties.Single(p => p.Name == "Route").Value.ToString().ShouldContain("/api/v1/products");
        http.Properties.Single(p => p.Name == "RemoteAddress").Value.ToString().ShouldContain("10.0.0.5");
        http.Properties.Single(p => p.Name == "UserAgent").Value.ToString().ShouldContain("test-agent");
        http.Properties.Single(p => p.Name == "StatusCode").Value.ToString().ShouldContain("201");
    }

    [Fact]
    public async Task InvokeAsync_WithoutRemoteAddress_DefaultsRemoteAddressToEmptyString()
    {
        var (logger, sink) = BuildRealLogger();
        var sut = new RequestLoggingMiddleware(_ => Task.CompletedTask, logger);
        var context = BuildContext();
        // No RemoteIpAddress and no UserAgent set — exercises the "?? string.Empty" branches.

        await sut.InvokeAsync(context);

        var http = sink.Events[0].Properties["http"] as StructureValue;
        http.ShouldNotBeNull();
        http!.Properties.Single(p => p.Name == "RemoteAddress").Value.ToString().ShouldContain("\"\"");
    }

    [Fact]
    public async Task InvokeAsync_WhenDownstreamPushesLogProperties_IncludesThemInCompletionLog()
    {
        var (logger, sink) = BuildRealLogger();
        var sut = new RequestLoggingMiddleware(ctx =>
        {
            ctx.PushLogProperties(new Dictionary<string, object?> { ["userId"] = "u1" });
            return Task.CompletedTask;
        }, logger);
        var context = BuildContext();

        await sut.InvokeAsync(context);

        sink.Events.Count.ShouldBe(1);
        var properties = sink.Events[0].Properties["properties"] as DictionaryValue;
        properties.ShouldNotBeNull();
        properties!.Elements.Single(kv => Equals(kv.Key.Value, "userId")).Value.ToString().ShouldContain("u1");
    }
}
