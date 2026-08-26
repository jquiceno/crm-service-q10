using System.Net;
using Shouldly;
using Xunit;

namespace IntegrationTests.Infrastructure;

[Collection(IntegrationTestCollection.Name)]
public sealed class HealthProbesTests : IntegrationTestBase
{
    public HealthProbesTests(SqlServerContainerFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Live_Returns200()
    {
        var response = await Client.GetAsync("/service-template/health/live");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Ready_Returns200()
    {
        var response = await Client.GetAsync("/service-template/health/ready");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
