using Shared.Presentation.Routing;
using Shouldly;
using Xunit;

namespace UnitTests.Shared.Presentation;

public sealed class KebabCaseParameterTransformerTests
{
    private readonly KebabCaseParameterTransformer _sut = new();

    [Fact]
    public void TransformOutbound_WhenValueIsNull_ReturnsNull()
    {
        var result = _sut.TransformOutbound(null);

        result.ShouldBeNull();
    }

    [Theory]
    [InlineData("AnnouncementId", "announcement-id")]
    [InlineData("OrdersController", "orders-controller")]
    [InlineData("webinarSession", "webinar-session")]
    [InlineData("HTTPServer", "httpserver")]
    [InlineData("Section2Header", "section2-header")]
    public void TransformOutbound_WhenValueIsString_ConvertsToKebabCase(string input, string expected)
    {
        var result = _sut.TransformOutbound(input);

        result.ShouldBe(expected);
    }

    [Fact]
    public void TransformOutbound_WhenValueIsNonString_UsesToStringRepresentation()
    {
        var result = _sut.TransformOutbound(123);

        result.ShouldBe("123");
    }
}
