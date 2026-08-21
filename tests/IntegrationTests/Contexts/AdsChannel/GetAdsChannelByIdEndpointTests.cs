using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AdsChannel.Application.UseCases.GetAdsChannelById;
using IntegrationTests.Infrastructure;
using Shouldly;
using Xunit;

namespace IntegrationTests.Contexts.AdsChannel;

[Collection(IntegrationTestCollection.Name)]
public sealed class GetAdsChannelByIdEndpointTests : IntegrationTestBase
{
    public GetAdsChannelByIdEndpointTests(SqlServerContainerFixture fixture) : base(fixture) { }

    [Fact]
    public async Task GetAdsChannelById_WithExistingId_Returns200WithMappedData()
    {
        var adsChannel = new global::Infrastructure.Persistence.EntityFramework.AdsChannels.Entities.AdsChannel
        {
            Name = "Google Ads",
            IsActive = true,
        };
        Db.AdsChannels.Add(adsChannel);
        await Db.SaveChangesAsync();

        var response = await Client.GetAsync($"/ads-channels/{adsChannel.Id}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<GetAdsChannelByIdOutputDto>>(
            JsonSerializerOptions.Web);
        body!.Data.Id.ShouldBe(adsChannel.Id);
        body.Data.Name.ShouldBe("Google Ads");
        body.Data.IsActive.ShouldBeTrue();
    }

    [Fact]
    public async Task GetAdsChannelById_WithNonexistentId_Returns404()
    {
        var response = await Client.GetAsync("/ads-channels/999999");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetAdsChannelById_CalledTwiceForSameId_ReturnsIdenticalResponsesBothOk()
    {
        // Smoke check for the [OutputCache] attribute that a later step will add to the controller
        // action: once wired, the second call should be served from cache but must still answer 200
        // with the same body as the first call. Without caching this simply verifies idempotency.
        var adsChannel = new global::Infrastructure.Persistence.EntityFramework.AdsChannels.Entities.AdsChannel
        {
            Name = "Meta Ads",
            IsActive = true,
        };
        Db.AdsChannels.Add(adsChannel);
        await Db.SaveChangesAsync();

        var firstResponse = await Client.GetAsync($"/ads-channels/{adsChannel.Id}");
        var secondResponse = await Client.GetAsync($"/ads-channels/{adsChannel.Id}");

        firstResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        secondResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var firstBody = await firstResponse.Content.ReadFromJsonAsync<ApiResponse<GetAdsChannelByIdOutputDto>>(
            JsonSerializerOptions.Web);
        var secondBody = await secondResponse.Content.ReadFromJsonAsync<ApiResponse<GetAdsChannelByIdOutputDto>>(
            JsonSerializerOptions.Web);

        secondBody.ShouldBe(firstBody);
    }
}
