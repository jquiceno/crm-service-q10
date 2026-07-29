using Infrastructure.Logging;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Shared.Application.Interfaces;
using Shouldly;
using Xunit;

namespace UnitTests.Infrastructure.Logging;

public sealed class LogContextExtensionsTests
{
    private sealed class CollectingSink : ILogEventSink
    {
        public List<LogEvent> Events { get; } = [];
        public void Emit(LogEvent logEvent) => Events.Add(logEvent);
    }

    private sealed class FakeLogProperties(IReadOnlyList<KeyValuePair<string, object?>> items) : ILogProperties
    {
        public IEnumerable<KeyValuePair<string, object?>> GetProperties() => items;
    }

    private static (ILogger Logger, CollectingSink Sink) BuildLogger()
    {
        var sink = new CollectingSink();
        var logger = new LoggerConfiguration()
            .Enrich.FromLogContext()
            .MinimumLevel.Verbose()
            .WriteTo.Sink(sink)
            .CreateLogger();
        return (logger, sink);
    }

    [Fact]
    public void PushHttpProperties_WithHttpRequestLogProperties_EnrichesLogsOnlyWithinScope()
    {
        var (logger, sink) = BuildLogger();
        var properties = new HttpRequestLogProperties("agent", "1.2.3.4", "GET", "/x", 200, 15);

        using (properties.PushHttpProperties())
        {
            logger.Information("inside");
        }
        logger.Information("outside");

        sink.Events.Count.ShouldBe(2);
        sink.Events[0].Properties.ContainsKey("http").ShouldBeTrue();
        var http = sink.Events[0].Properties["http"] as StructureValue;
        http.ShouldNotBeNull();
        http!.Properties.Single(p => p.Name == "StatusCode").Value.ToString().ShouldContain("200");
        sink.Events[1].Properties.ContainsKey("http").ShouldBeFalse();
    }

    [Fact]
    public void PushHttpProperties_WithHttpContextProperties_EnrichesLogsOnlyWithinScope()
    {
        var (logger, sink) = BuildLogger();
        var properties = new HttpContextProperties("agent", "::1", "POST", "/y");

        using (properties.PushHttpProperties())
        {
            logger.Information("inside");
        }
        logger.Information("outside");

        var http = sink.Events[0].Properties["http"] as StructureValue;
        http.ShouldNotBeNull();
        http!.Properties.Single(p => p.Name == "Method").Value.ToString().ShouldContain("POST");
        sink.Events[1].Properties.ContainsKey("http").ShouldBeFalse();
    }

    [Fact]
    public void PushProperties_WithDictionary_EnrichesLogWithPropertiesKey()
    {
        var (logger, sink) = BuildLogger();
        IReadOnlyDictionary<string, object?> properties = new Dictionary<string, object?>
        {
            ["userId"] = "u1",
            ["count"] = 3,
        };

        using (properties.PushProperties())
        {
            logger.Information("inside");
        }

        var dictionary = sink.Events[0].Properties["properties"] as DictionaryValue;
        dictionary.ShouldNotBeNull();
        dictionary!.Elements.Single(kv => Equals(kv.Key.Value, "userId")).Value.ToString().ShouldContain("u1");
    }

    [Fact]
    public void PushFlatProperties_WithILogProperties_PushesEachKeyAsSeparateProperty()
    {
        var (logger, sink) = BuildLogger();
        var fake = new FakeLogProperties(
        [
            new KeyValuePair<string, object?>("userId", "u1"),
            new KeyValuePair<string, object?>("count", 5),
        ]);

        using (fake.PushFlatProperties())
        {
            logger.Information("inside");
        }
        logger.Information("outside");

        var insideEvent = sink.Events[0];
        insideEvent.Properties["userId"].ToString().ShouldContain("u1");
        insideEvent.Properties["count"].ToString().ShouldContain("5");

        var outsideEvent = sink.Events[1];
        outsideEvent.Properties.ContainsKey("userId").ShouldBeFalse();
        outsideEvent.Properties.ContainsKey("count").ShouldBeFalse();
    }

    [Fact]
    public void PushFlatProperties_WhenDisposedTwice_DoesNotThrowAndDisposesOnlyOnce()
    {
        var (logger, sink) = BuildLogger();
        var fake = new FakeLogProperties([new KeyValuePair<string, object?>("flag", true)]);

        var scope = fake.PushFlatProperties();
        scope.Dispose();
        scope.Dispose();

        logger.Information("after-dispose");

        sink.Events[0].Properties.ContainsKey("flag").ShouldBeFalse();
    }

    [Fact]
    public void PushFlatProperties_WithNoProperties_ReturnsDisposableThatPushesNothing()
    {
        var (logger, sink) = BuildLogger();
        var fake = new FakeLogProperties([]);

        using (fake.PushFlatProperties())
        {
            logger.Information("inside");
        }

        sink.Events[0].Properties.Count.ShouldBe(0);
    }
}
