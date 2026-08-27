using Microsoft.Extensions.Configuration;
using Shared.Presentation.Routing;
using Shouldly;
using Xunit;

namespace UnitTests.Shared.Presentation;

public sealed class RoutePrefixConfigTests
{
    [Theory]
    [InlineData("/service-template", "service-template")]
    [InlineData("service-template", "service-template")]
    [InlineData("/service-template/", "service-template")]
    [InlineData(" /service-template/ ", "service-template")]
    [InlineData("   ", "")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void Normalize_TrimsWhitespaceAndSlashes(string? input, string expected) =>
        RoutePrefixConfig.Normalize(input).ShouldBe(expected);

    [Theory]
    [InlineData("/service-template", "/service-template")]
    [InlineData("service-template", "/service-template")]
    [InlineData("service-template/", "/service-template")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void BasePath_HasSingleLeadingSlash_OrEmpty(string? input, string expected) =>
        RoutePrefixConfig.BasePath(input).ShouldBe(expected);

    [Fact]
    public void GetRoutePrefix_ReadsAndNormalizesTheConfigKey()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["RoutePrefix"] = "/service-template/" })
            .Build();

        config.GetRoutePrefix().ShouldBe("service-template");
    }

    [Fact]
    public void GetRoutePrefix_WhenMissing_ReturnsEmpty()
    {
        var config = new ConfigurationBuilder().Build();

        config.GetRoutePrefix().ShouldBe(string.Empty);
    }
}
