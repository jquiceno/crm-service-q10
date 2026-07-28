using System.Diagnostics;
using Infrastructure.Logging;
using NSubstitute;
using Serilog.Core;
using Serilog.Events;
using Serilog.Parsing;
using Shouldly;
using Xunit;

namespace UnitTests.Infrastructure.Logging;

public sealed class ActivityEnricherTests
{
    private readonly ActivityEnricher _sut = new();
    private static readonly MessageTemplateParser TemplateParser = new();

    private static ILogEventPropertyFactory BuildFactory()
    {
        var factory = Substitute.For<ILogEventPropertyFactory>();
        factory.CreateProperty(Arg.Any<string>(), Arg.Any<object>(), Arg.Any<bool>())
            .Returns(callInfo => new LogEventProperty((string)callInfo[0], new ScalarValue(callInfo[1])));
        return factory;
    }

    private static LogEvent CreateLogEvent(IEnumerable<LogEventProperty>? properties = null) =>
        new(
            DateTimeOffset.UtcNow,
            LogEventLevel.Information,
            exception: null,
            TemplateParser.Parse("test"),
            properties ?? []);

    private static CompositeDisposable StartActivity(string sourceName, out Activity activity)
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
        };
        ActivitySource.AddActivityListener(listener);
        var source = new ActivitySource(sourceName);
        var started = source.StartActivity("test-activity");
        started.ShouldNotBeNull();
        activity = started!;
        return new CompositeDisposable(activity, source, listener);
    }

    private sealed class CompositeDisposable(params IDisposable[] disposables) : IDisposable
    {
        public void Dispose()
        {
            foreach (var disposable in disposables)
                disposable.Dispose();
        }
    }

    [Fact]
    public void Enrich_WhenNoActivityIsCurrent_DoesNotAddTraceOrSpanProperties()
    {
        Activity.Current = null;
        var logEvent = CreateLogEvent();
        var factory = BuildFactory();

        _sut.Enrich(logEvent, factory);

        logEvent.Properties.ContainsKey("traceId").ShouldBeFalse();
        logEvent.Properties.ContainsKey("spanId").ShouldBeFalse();
    }

    [Fact]
    public void Enrich_WhenActivityIsCurrent_AddsTraceIdAndSpanId()
    {
        using var scope = StartActivity(nameof(Enrich_WhenActivityIsCurrent_AddsTraceIdAndSpanId), out var activity);
        var logEvent = CreateLogEvent();
        var factory = BuildFactory();

        _sut.Enrich(logEvent, factory);

        logEvent.Properties.ContainsKey("traceId").ShouldBeTrue();
        logEvent.Properties.ContainsKey("spanId").ShouldBeTrue();
        logEvent.Properties["traceId"].ToString().ShouldContain(activity.TraceId.ToString());
        logEvent.Properties["spanId"].ToString().ShouldContain(activity.SpanId.ToString());
    }

    [Fact]
    public void Enrich_WhenTraceIdPropertyAlreadyPresent_DoesNotOverwriteExistingValue()
    {
        using var scope = StartActivity(
            nameof(Enrich_WhenTraceIdPropertyAlreadyPresent_DoesNotOverwriteExistingValue), out _);
        var existing = new LogEventProperty("traceId", new ScalarValue("pre-existing"));
        var logEvent = CreateLogEvent([existing]);
        var factory = BuildFactory();

        _sut.Enrich(logEvent, factory);

        logEvent.Properties["traceId"].ToString().ShouldContain("pre-existing");
        // spanId is still absent beforehand, so it is added normally.
        logEvent.Properties.ContainsKey("spanId").ShouldBeTrue();
    }
}
