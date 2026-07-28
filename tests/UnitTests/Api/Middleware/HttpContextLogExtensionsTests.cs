using Api.Middleware;
using Microsoft.AspNetCore.Http;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Shouldly;
using Xunit;

namespace UnitTests.Api.Middleware;

public sealed class HttpContextLogExtensionsTests
{
    private sealed class CollectingSink : ILogEventSink
    {
        public List<LogEvent> Events { get; } = [];
        public void Emit(LogEvent logEvent) => Events.Add(logEvent);
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
    public void PushLogProperties_CalledOnce_PushesPropertiesUnderPropertiesKeyWithinScope()
    {
        var (logger, sink) = BuildLogger();
        var context = new DefaultHttpContext();

        using (context.PushLogProperties(new Dictionary<string, object?> { ["userId"] = "u1" }))
        {
            logger.Information("inside");
        }
        logger.Information("outside");

        var insideProperties = sink.Events[0].Properties["properties"] as DictionaryValue;
        insideProperties.ShouldNotBeNull();
        insideProperties!.Elements.Single(kv => Equals(kv.Key.Value, "userId")).Value.ToString().ShouldContain("u1");
        sink.Events[1].Properties.ContainsKey("properties").ShouldBeFalse();
    }

    [Fact]
    public void PushLogProperties_CalledTwiceOnSameContext_AccumulatesKeysFromBothCalls()
    {
        var (logger, sink) = BuildLogger();
        var context = new DefaultHttpContext();

        using var firstScope = context.PushLogProperties(new Dictionary<string, object?> { ["a"] = 1 });
        using var secondScope = context.PushLogProperties(new Dictionary<string, object?> { ["b"] = 2 });
        logger.Information("test");

        var properties = sink.Events[0].Properties["properties"] as DictionaryValue;
        properties.ShouldNotBeNull();
        properties!.Elements.Single(kv => Equals(kv.Key.Value, "a")).Value.ToString().ShouldContain("1");
        properties.Elements.Single(kv => Equals(kv.Key.Value, "b")).Value.ToString().ShouldContain("2");
    }

    [Fact]
    public void PushLogProperties_CalledTwiceWithOverlappingKey_SecondCallOverwritesTheFirst()
    {
        var (logger, sink) = BuildLogger();
        var context = new DefaultHttpContext();

        using var firstScope = context.PushLogProperties(new Dictionary<string, object?> { ["status"] = "pending" });
        using var secondScope = context.PushLogProperties(new Dictionary<string, object?> { ["status"] = "done" });
        logger.Information("test");

        var properties = sink.Events[0].Properties["properties"] as DictionaryValue;
        properties.ShouldNotBeNull();
        properties!.Elements.Single(kv => Equals(kv.Key.Value, "status")).Value.ToString().ShouldContain("done");
    }

    [Fact]
    public void PushLogProperties_AfterScopeDisposed_NoLongerAppearsInSubsequentLogs()
    {
        var (logger, sink) = BuildLogger();
        var context = new DefaultHttpContext();

        var scope = context.PushLogProperties(new Dictionary<string, object?> { ["userId"] = "u1" });
        scope.Dispose();
        logger.Information("after-dispose");

        sink.Events[0].Properties.ContainsKey("properties").ShouldBeFalse();
    }
}
