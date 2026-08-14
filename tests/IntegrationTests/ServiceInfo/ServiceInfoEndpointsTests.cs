using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using IntegrationTests.Infrastructure;
using ServiceInfo.Application.UseCases.GetServiceInfo;
using Shouldly;
using Xunit;

namespace IntegrationTests.ServiceInfo;

[Collection(IntegrationTestCollection.Name)]
public sealed class ServiceInfoEndpointsTests : IntegrationTestBase
{
    public ServiceInfoEndpointsTests(SqlServerContainerFixture fixture) : base(fixture) { }

    [Fact]
    public async Task GetInfo_Returns200_WithServiceInfo()
    {
        var response = await Client.GetAsync("/info");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<GetServiceInfoOutputDto>>(JsonSerializerOptions.Web);
        body!.Data.Status.ShouldBe("ok");
        body.Data.Name.ShouldNotBeNullOrEmpty();
        body.Data.Version.ShouldNotBeNullOrEmpty();
    }
}
