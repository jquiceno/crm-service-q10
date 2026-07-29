using Infrastructure.Adapters.Logging;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Shouldly;
using Xunit;

namespace UnitTests.Infrastructure.Logging;

public sealed class SerilogLoggerAdapterTests
{
    private sealed class Marker { }

    private sealed class CollectingSink : ILogEventSink
    {
        public List<LogEvent> Events { get; } = [];
        public void Emit(LogEvent logEvent) => Events.Add(logEvent);
    }

    private static (SerilogLoggerAdapter<Marker> Sut, CollectingSink Sink) BuildSut()
    {
        var sink = new CollectingSink();
        var logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.Sink(sink)
            .CreateLogger();
        return (new SerilogLoggerAdapter<Marker>(logger), sink);
    }

    [Fact]
    public void Debug_WithMessageAndArgs_EmitsDebugLevelEventWithSourceContext()
    {
        var (sut, sink) = BuildSut();

        sut.Debug("User {UserId} loaded", 42);

        sink.Events.Count.ShouldBe(1);
        var loggedEvent = sink.Events[0];
        loggedEvent.Level.ShouldBe(LogEventLevel.Debug);
        loggedEvent.RenderMessage().ShouldBe("User 42 loaded");
        loggedEvent.Properties.ContainsKey("SourceContext").ShouldBeTrue();
        loggedEvent.Properties["SourceContext"].ToString().ShouldContain(nameof(Marker));
    }

    [Fact]
    public void Info_WithMessageAndArgs_EmitsInformationLevelEvent()
    {
        var (sut, sink) = BuildSut();

        sut.Info("Product {Id} created", 7);

        sink.Events.Count.ShouldBe(1);
        var loggedEvent = sink.Events[0];
        loggedEvent.Level.ShouldBe(LogEventLevel.Information);
        loggedEvent.RenderMessage().ShouldBe("Product 7 created");
    }

    [Fact]
    public void Warning_WithMessageAndArgs_EmitsWarningLevelEventWithoutException()
    {
        var (sut, sink) = BuildSut();

        sut.Warning("Low stock for {Sku}", "ABC");

        sink.Events.Count.ShouldBe(1);
        var loggedEvent = sink.Events[0];
        loggedEvent.Level.ShouldBe(LogEventLevel.Warning);
        loggedEvent.Exception.ShouldBeNull();
        loggedEvent.RenderMessage().ShouldContain("ABC");
    }

    [Fact]
    public void Warning_WithExceptionAndArgs_EmitsWarningLevelEventWithException()
    {
        var (sut, sink) = BuildSut();
        var exception = new InvalidOperationException("boom");

        sut.Warning(exception, "Retry {Attempt}", 2);

        sink.Events.Count.ShouldBe(1);
        var loggedEvent = sink.Events[0];
        loggedEvent.Level.ShouldBe(LogEventLevel.Warning);
        loggedEvent.Exception.ShouldBeSameAs(exception);
    }

    [Fact]
    public void Warning_WithNullException_EmitsWarningLevelEventWithoutException()
    {
        var (sut, sink) = BuildSut();

        sut.Warning(exception: null, "Retry {Attempt}", 3);

        sink.Events.Count.ShouldBe(1);
        sink.Events[0].Exception.ShouldBeNull();
    }

    [Fact]
    public void Error_WithExceptionAndArgs_EmitsErrorLevelEventWithException()
    {
        var (sut, sink) = BuildSut();
        var exception = new InvalidOperationException("boom");

        sut.Error(exception, "Failed to process {Id}", 99);

        sink.Events.Count.ShouldBe(1);
        var loggedEvent = sink.Events[0];
        loggedEvent.Level.ShouldBe(LogEventLevel.Error);
        loggedEvent.Exception.ShouldBeSameAs(exception);
        loggedEvent.RenderMessage().ShouldBe("Failed to process 99");
    }

    [Fact]
    public void Error_WithNullException_EmitsErrorLevelEventWithoutException()
    {
        var (sut, sink) = BuildSut();

        sut.Error(exception: null, "Failed to process {Id}", 100);

        sink.Events.Count.ShouldBe(1);
        sink.Events[0].Exception.ShouldBeNull();
    }
}
