using System.Globalization;
using System.Text.Json;
using Infrastructure.Logging;
using Serilog.Events;
using Serilog.Parsing;
using Shouldly;
using Xunit;

namespace UnitTests.Infrastructure.Logging;

public sealed class FlatJsonFormatterTests
{
    private readonly FlatJsonFormatter _sut = new();
    private static readonly MessageTemplateParser TemplateParser = new();

    private static LogEvent CreateLogEvent(
        string messageTemplate = "Hello",
        LogEventLevel level = LogEventLevel.Information,
        Exception? exception = null,
        IEnumerable<LogEventProperty>? properties = null,
        DateTimeOffset? timestamp = null) =>
        new(
            timestamp ?? DateTimeOffset.Parse("2026-01-15T10:30:00.1234567Z", CultureInfo.InvariantCulture),
            level,
            exception,
            TemplateParser.Parse(messageTemplate),
            properties ?? []);

    private JsonDocument Format(LogEvent logEvent)
    {
        var writer = new StringWriter();
        _sut.Format(logEvent, writer);
        return JsonDocument.Parse(writer.ToString());
    }

    [Fact]
    public void Format_WithSimpleMessage_WritesMessageTimestampAndLevel()
    {
        var logEvent = CreateLogEvent("Hello world", LogEventLevel.Information);

        using var doc = Format(logEvent);
        var root = doc.RootElement;

        root.GetProperty("message").GetString().ShouldBe("Hello world");
        root.GetProperty("level").GetString().ShouldBe("information");
        var timestamp = DateTime.Parse(
            root.GetProperty("timestamp").GetString()!,
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind);
        timestamp.ShouldBe(logEvent.Timestamp.UtcDateTime);
    }

    [Fact]
    public void Format_WithoutException_DoesNotWriteExceptionField()
    {
        var logEvent = CreateLogEvent();

        using var doc = Format(logEvent);

        doc.RootElement.TryGetProperty("exception", out _).ShouldBeFalse();
    }

    [Fact]
    public void Format_WithException_WritesExceptionField()
    {
        var exception = new InvalidOperationException("boom");
        var logEvent = CreateLogEvent("Failure", LogEventLevel.Error, exception);

        using var doc = Format(logEvent);

        var exceptionText = doc.RootElement.GetProperty("exception").GetString();
        exceptionText.ShouldNotBeNull();
        exceptionText.ShouldContain("InvalidOperationException");
        exceptionText.ShouldContain("boom");
    }

    [Theory]
    [InlineData(LogEventLevel.Verbose, "verbose")]
    [InlineData(LogEventLevel.Debug, "debug")]
    [InlineData(LogEventLevel.Information, "information")]
    [InlineData(LogEventLevel.Warning, "warning")]
    [InlineData(LogEventLevel.Error, "error")]
    [InlineData(LogEventLevel.Fatal, "fatal")]
    public void Format_WithVariousLevels_WritesLowercaseLevel(LogEventLevel level, string expected)
    {
        var logEvent = CreateLogEvent(level: level);

        using var doc = Format(logEvent);

        doc.RootElement.GetProperty("level").GetString().ShouldBe(expected);
    }

    [Fact]
    public void Format_WithScalarStringPropertyContainingSpecialCharacters_EscapesCorrectly()
    {
        const string value = "line1\nline2\t\"quoted\"\\backslashé";
        var properties = new[] { new LogEventProperty("SpecialChars", new ScalarValue(value)) };
        var logEvent = CreateLogEvent(properties: properties);

        using var doc = Format(logEvent);

        doc.RootElement.GetProperty("specialChars").GetString().ShouldBe(value);
    }

    [Fact]
    public void Format_WithNumericBooleanAndNullScalarProperties_WritesCorrectJsonTypes()
    {
        var properties = new[]
        {
            new LogEventProperty("Count", new ScalarValue(42)),
            new LogEventProperty("IsActive", new ScalarValue(true)),
            new LogEventProperty("Missing", new ScalarValue(null)),
        };
        var logEvent = CreateLogEvent(properties: properties);

        using var doc = Format(logEvent);
        var root = doc.RootElement;

        root.GetProperty("count").GetInt32().ShouldBe(42);
        root.GetProperty("isActive").GetBoolean().ShouldBeTrue();
        root.GetProperty("missing").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public void Format_WithLowercasePropertyName_DoesNotChangeCasing()
    {
        var properties = new[] { new LogEventProperty("alreadyLower", new ScalarValue("x")) };
        var logEvent = CreateLogEvent(properties: properties);

        using var doc = Format(logEvent);

        doc.RootElement.TryGetProperty("alreadyLower", out var element).ShouldBeTrue();
        element.GetString().ShouldBe("x");
    }

    [Fact]
    public void Format_WithStructuredProperty_WritesNestedObjectWithCamelCaseKeys()
    {
        var structure = new StructureValue(new[]
        {
            new LogEventProperty("UserId", new ScalarValue("u1")),
            new LogEventProperty("Age", new ScalarValue(30)),
        });
        var properties = new[] { new LogEventProperty("User", structure) };
        var logEvent = CreateLogEvent(properties: properties);

        using var doc = Format(logEvent);
        var user = doc.RootElement.GetProperty("user");

        user.GetProperty("userId").GetString().ShouldBe("u1");
        user.GetProperty("age").GetInt32().ShouldBe(30);
    }

    [Fact]
    public void Format_WithEmptyStructuredProperty_WritesEmptyObject()
    {
        var structure = new StructureValue([]);
        var properties = new[] { new LogEventProperty("Empty", structure) };
        var logEvent = CreateLogEvent(properties: properties);

        using var doc = Format(logEvent);
        var empty = doc.RootElement.GetProperty("empty");

        empty.ValueKind.ShouldBe(JsonValueKind.Object);
        empty.EnumerateObject().Any().ShouldBeFalse();
    }

    [Fact]
    public void Format_WithSequenceProperty_WritesJsonArray()
    {
        var sequence = new SequenceValue(new LogEventPropertyValue[]
        {
            new ScalarValue(1),
            new ScalarValue(2),
            new ScalarValue(3),
        });
        var properties = new[] { new LogEventProperty("Numbers", sequence) };
        var logEvent = CreateLogEvent(properties: properties);

        using var doc = Format(logEvent);
        var numbers = doc.RootElement.GetProperty("numbers");

        numbers.ValueKind.ShouldBe(JsonValueKind.Array);
        numbers.EnumerateArray().Select(e => e.GetInt32()).ToArray().ShouldBe([1, 2, 3]);
    }

    [Fact]
    public void Format_WithEmptySequenceProperty_WritesEmptyArray()
    {
        var sequence = new SequenceValue([]);
        var properties = new[] { new LogEventProperty("Empty", sequence) };
        var logEvent = CreateLogEvent(properties: properties);

        using var doc = Format(logEvent);
        var empty = doc.RootElement.GetProperty("empty");

        empty.ValueKind.ShouldBe(JsonValueKind.Array);
        empty.GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public void Format_WithSequenceOfStructures_WritesNestedArrayOfObjects()
    {
        var sequence = new SequenceValue(new LogEventPropertyValue[]
        {
            new StructureValue(new[] { new LogEventProperty("Id", new ScalarValue(1)) }),
            new StructureValue(new[] { new LogEventProperty("Id", new ScalarValue(2)) }),
        });
        var properties = new[] { new LogEventProperty("Items", sequence) };
        var logEvent = CreateLogEvent(properties: properties);

        using var doc = Format(logEvent);
        var items = doc.RootElement.GetProperty("items");

        items.GetArrayLength().ShouldBe(2);
        items[0].GetProperty("id").GetInt32().ShouldBe(1);
        items[1].GetProperty("id").GetInt32().ShouldBe(2);
    }

    [Fact]
    public void Format_WithDictionaryProperty_WritesJsonObjectWithoutCamelCasingKeys()
    {
        var dictionary = new DictionaryValue(new[]
        {
            new KeyValuePair<ScalarValue, LogEventPropertyValue>(new ScalarValue("UserId"), new ScalarValue("u1")),
            new KeyValuePair<ScalarValue, LogEventPropertyValue>(new ScalarValue("Count"), new ScalarValue(2)),
        });
        var properties = new[] { new LogEventProperty("Meta", dictionary) };
        var logEvent = CreateLogEvent(properties: properties);

        using var doc = Format(logEvent);
        var meta = doc.RootElement.GetProperty("meta");

        // Dictionary keys are written verbatim (not camelCased) — only top-level
        // property names and nested structure property names go through ToCamelCase.
        meta.GetProperty("UserId").GetString().ShouldBe("u1");
        meta.GetProperty("Count").GetInt32().ShouldBe(2);
    }

    [Fact]
    public void Format_WithMultipleProperties_ProducesValidJsonWithAllFields()
    {
        var properties = new[]
        {
            new LogEventProperty("First", new ScalarValue(1)),
            new LogEventProperty("Second", new ScalarValue("two")),
            new LogEventProperty("Third", new ScalarValue(true)),
        };
        var logEvent = CreateLogEvent("multi", properties: properties);

        using var doc = Format(logEvent);
        var root = doc.RootElement;

        root.GetProperty("first").GetInt32().ShouldBe(1);
        root.GetProperty("second").GetString().ShouldBe("two");
        root.GetProperty("third").GetBoolean().ShouldBeTrue();
    }
}
