using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AdsChannel.Application.UseCases.GetAdsChannelById;
using IntegrationTests.Infrastructure;
using Shared.Presentation.Responses;
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

        var body = await response.Content.ReadFromJsonAsync<ApiErrorResponse>(JsonSerializerOptions.Web);
        body!.Error.Type.ShouldBe("NOT_FOUND");
        body.StatusCode.ShouldBe(404);
    }

    [Fact]
    public async Task GetAdsChannelById_CalledTwiceForSameId_ServesSecondCallFromCache()
    {
        var adsChannel = new global::Infrastructure.Persistence.EntityFramework.AdsChannels.Entities.AdsChannel
        {
            Name = "Meta Ads",
            IsActive = true,
        };
        Db.AdsChannels.Add(adsChannel);
        await Db.SaveChangesAsync();

        var firstResponse = await Client.GetAsync($"/ads-channels/{adsChannel.Id}");
        firstResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var firstBody = await firstResponse.Content.ReadFromJsonAsync<ApiResponse<GetAdsChannelByIdOutputDto>>(
            JsonSerializerOptions.Web);

        // Mutate the row directly, bypassing the use case (and its cache-invalidation tag), so the
        // only way the second call could observe the new name is by skipping [OutputCache] entirely.
        adsChannel.Name = "Google Ads";
        await Db.SaveChangesAsync();

        var secondResponse = await Client.GetAsync($"/ads-channels/{adsChannel.Id}");
        secondResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var secondBody = await secondResponse.Content.ReadFromJsonAsync<ApiResponse<GetAdsChannelByIdOutputDto>>(
            JsonSerializerOptions.Web);

        secondBody.ShouldBe(firstBody);
        secondBody!.Data.Name.ShouldBe("Meta Ads");
    }
}
