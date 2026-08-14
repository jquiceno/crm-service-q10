using System.Text.Json;
using System.Text.Json.Serialization;
using Shared.Presentation.Filters;
using Shouldly;
using Xunit;

namespace UnitTests.Shared.Presentation;

public sealed class JsonTypeScannerTests
{
    private enum SampleStatus
    {
        Active,
        Inactive
    }

    private sealed class Address
    {
        public string City { get; set; } = string.Empty;
        public string Zip { get; set; } = string.Empty;
    }

    private sealed class SampleModel
    {
        public string Name { get; set; } = string.Empty;
        public int Age { get; set; }
        public bool IsActive { get; set; }
        public Guid Id { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
        public DateOnly BirthDate { get; set; }
        public TimeOnly Time { get; set; }
        public TimeSpan Duration { get; set; }
        public Uri Website { get; set; } = new("https://example.com");
        public SampleStatus Status { get; set; }
        public List<string> Tags { get; set; } = [];
        public Address? HomeAddress { get; set; }
        public char Initial { get; set; }
        public int? NullableAge { get; set; }

        [JsonPropertyName("custom_name")]
        public string CustomProp { get; set; } = string.Empty;
    }

    [Fact]
    public void Scan_WhenRootIsNotObject_ReturnsEmpty()
    {
        using var doc = JsonDocument.Parse("[1,2,3]");

        var result = JsonTypeScanner.Scan(doc.RootElement, typeof(SampleModel));

        result.ShouldBeEmpty();
    }

    [Fact]
    public void Scan_WhenDepthAtMax_ReturnsEmptyImmediately()
    {
        using var doc = JsonDocument.Parse("""{"age":"not-a-number"}""");

        var result = JsonTypeScanner.Scan(doc.RootElement, typeof(SampleModel), depth: 32);

        result.ShouldBeEmpty();
    }

    [Fact]
    public void Scan_WhenPropertyValueIsNull_SkipsProperty()
    {
        using var doc = JsonDocument.Parse("""{"age":null}""");

        var result = JsonTypeScanner.Scan(doc.RootElement, typeof(SampleModel));

        result.ShouldBeEmpty();
    }

    [Fact]
    public void Scan_WhenJsonHasUnknownProperty_IgnoresIt()
    {
        using var doc = JsonDocument.Parse("""{"unknownProp":"x"}""");

        var result = JsonTypeScanner.Scan(doc.RootElement, typeof(SampleModel));

        result.ShouldBeEmpty();
    }

    [Fact]
    public void Scan_WhenAllPropertiesCompatible_ReturnsEmpty()
    {
        const string json = """
        {
          "name": "Alice",
          "age": 30,
          "isActive": true,
          "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
          "createdAt": "2024-01-01T00:00:00Z",
          "updatedAt": "2024-01-01T00:00:00Z",
          "birthDate": "2024-01-01",
          "time": "10:00:00",
          "duration": "01:00:00",
          "website": "https://example.com",
          "status": 0,
          "tags": ["a", "b"],
          "homeAddress": {"city": "NY", "zip": "10001"},
          "custom_name": "x",
          "initial": "A",
          "nullableAge": 5
        }
        """;
        using var doc = JsonDocument.Parse(json);

        var result = JsonTypeScanner.Scan(doc.RootElement, typeof(SampleModel));

        result.ShouldBeEmpty();
    }

    [Fact]
    public void Scan_WhenEnumPropertyProvidedAsString_IsCompatible()
    {
        using var doc = JsonDocument.Parse("""{"status":"Active"}""");

        var result = JsonTypeScanner.Scan(doc.RootElement, typeof(SampleModel));

        result.ShouldBeEmpty();
    }

    [Fact]
    public void Scan_WhenPropertyHasJsonPropertyNameAttribute_UsesAttributeNameForLookupAndCamelCasePropertyName()
    {
        using var doc = JsonDocument.Parse("""{"custom_name":123}""");

        var result = JsonTypeScanner.Scan(doc.RootElement, typeof(SampleModel));

        result.Count.ShouldBe(1);
        result[0].Property.ShouldBe("customProp");
    }

    [Theory]
    [InlineData("""{"name":123}""", "name", "Expected a string.")]
    [InlineData("""{"age":"x"}""", "age", "Expected a number.")]
    [InlineData("""{"isActive":"x"}""", "isActive", "Expected a boolean.")]
    [InlineData("""{"id":123}""", "id", "Expected a GUID string.")]
    [InlineData("""{"createdAt":123}""", "createdAt", "Expected a date-time string.")]
    [InlineData("""{"birthDate":123}""", "birthDate", "Expected a date string.")]
    [InlineData("""{"time":123}""", "time", "Expected a time string.")]
    [InlineData("""{"website":{}}""", "website", "Expected a URI string.")]
    [InlineData("""{"status":true}""", "status", "Expected a valid enum value.")]
    [InlineData("""{"homeAddress":"foo"}""", "homeAddress", "Expected a valid value.")]
    public void Scan_WhenPropertyTypeMismatched_ReturnsExpectedFriendlyMessage(
        string json, string property, string expectedMessage)
    {
        using var doc = JsonDocument.Parse(json);

        var result = JsonTypeScanner.Scan(doc.RootElement, typeof(SampleModel));

        result.Count.ShouldBe(1);
        result[0].Property.ShouldBe(property);
        result[0].Message.ShouldBe(expectedMessage);
    }

    [Fact]
    public void Scan_WhenMismatchedValueIsString_ExtractsStringValue()
    {
        using var doc = JsonDocument.Parse("""{"age":"abc"}""");

        var result = JsonTypeScanner.Scan(doc.RootElement, typeof(SampleModel));

        result[0].Value.ShouldBe("abc");
    }

    [Fact]
    public void Scan_WhenMismatchedValueIsInteger_ExtractsInt64Value()
    {
        using var doc = JsonDocument.Parse("""{"name":42}""");

        var result = JsonTypeScanner.Scan(doc.RootElement, typeof(SampleModel));

        result[0].Value.ShouldBe(42L);
    }

    [Fact]
    public void Scan_WhenMismatchedValueIsDecimal_ExtractsDecimalValue()
    {
        using var doc = JsonDocument.Parse("""{"name":42.5}""");

        var result = JsonTypeScanner.Scan(doc.RootElement, typeof(SampleModel));

        result[0].Value.ShouldBe(42.5m);
    }

    [Fact]
    public void Scan_WhenMismatchedValueIsHugeNumber_ExtractsRawText()
    {
        using var doc = JsonDocument.Parse("""{"name":1e400}""");

        var result = JsonTypeScanner.Scan(doc.RootElement, typeof(SampleModel));

        result[0].Value.ShouldBe("1e400");
    }

    [Fact]
    public void Scan_WhenMismatchedValueIsBoolTrue_ExtractsBoolValue()
    {
        using var doc = JsonDocument.Parse("""{"age":true}""");

        var result = JsonTypeScanner.Scan(doc.RootElement, typeof(SampleModel));

        result[0].Value.ShouldBe(true);
    }

    [Fact]
    public void Scan_WhenMismatchedValueIsArray_ExtractsClonedElement()
    {
        using var doc = JsonDocument.Parse("""{"age":[1,2,3]}""");

        var result = JsonTypeScanner.Scan(doc.RootElement, typeof(SampleModel));

        result[0].Value.ShouldBeOfType<JsonElement>();
    }

    [Fact]
    public void Scan_WhenMismatchedValueIsObject_ExtractsClonedElement()
    {
        using var doc = JsonDocument.Parse("""{"age":{}}""");

        var result = JsonTypeScanner.Scan(doc.RootElement, typeof(SampleModel));

        result[0].Value.ShouldBeOfType<JsonElement>();
    }

    [Fact]
    public void Scan_WhenNestedComplexTypeHasErrors_ReturnsErrorWithChildren()
    {
        using var doc = JsonDocument.Parse("""{"homeAddress":{"city":123}}""");

        var result = JsonTypeScanner.Scan(doc.RootElement, typeof(SampleModel));

        result.Count.ShouldBe(1);
        result[0].Property.ShouldBe("homeAddress");
        result[0].Message.ShouldBe("Validation failed");
        result[0].Children.ShouldNotBeNull();
        result[0].Children!.Count.ShouldBe(1);
        result[0].Children![0].Property.ShouldBe("city");
    }

    [Fact]
    public void Scan_WhenNestedComplexTypeValid_ReturnsEmpty()
    {
        using var doc = JsonDocument.Parse("""{"homeAddress":{"city":"NY","zip":"10001"}}""");

        var result = JsonTypeScanner.Scan(doc.RootElement, typeof(SampleModel));

        result.ShouldBeEmpty();
    }
}
