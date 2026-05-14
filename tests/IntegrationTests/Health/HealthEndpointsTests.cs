using System.Net;
using IntegrationTests.Infrastructure;
using Shouldly;
using Xunit;

namespace IntegrationTests.Health;

[Collection(IntegrationTestCollection.Name)]
public sealed class HealthEndpointsTests : IntegrationTestBase
{
    public HealthEndpointsTests(SqlServerContainerFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Live_Returns200()
    {
        var response = await Client.GetAsync("/health/live");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Ready_Returns200_WhenDatabaseIsAvailable()
    {
        var response = await Client.GetAsync("/health/ready");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
