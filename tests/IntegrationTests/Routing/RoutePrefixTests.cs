using System.Linq;
using IntegrationTests.Caching;
using IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace IntegrationTests.Routing;

[Collection(IntegrationTestCollection.Name)]
public sealed class RoutePrefixTests : IntegrationTestBase
{
    public RoutePrefixTests(SqlServerContainerFixture fixture, RedisContainerFixture cache) : base(fixture, cache) { }

    [Fact]
    public void EveryRegisteredEndpoint_IsUnderTheRoutePrefix()
    {
        var prefix = RoutePrefix;

        var offenders = Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Select(e => (e.RoutePattern.RawText ?? string.Empty).Trim('/'))
            .Where(raw => raw.Length > 0)
            .Where(raw => raw != prefix && !raw.StartsWith($"{prefix}/", StringComparison.Ordinal))
            .ToList();

        offenders.ShouldBeEmpty();
    }
}
