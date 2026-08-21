using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AdsChannel.Application.UseCases.GetAdsChannels;
using IntegrationTests.Infrastructure;
using Shouldly;
using Xunit;

namespace IntegrationTests.Contexts.AdsChannel;

[Collection(IntegrationTestCollection.Name)]
public sealed class GetAdsChannelsEndpointTests : IntegrationTestBase
{
    public GetAdsChannelsEndpointTests(SqlServerContainerFixture fixture) : base(fixture) { }

    [Fact]
    public async Task GetAdsChannels_WithoutFilters_Returns200WithAllSeededItemsAndTotalCount()
    {
        await SeedAsync();

        var response = await Client.GetAsync("/ads-channels");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<ApiPagedData<AdsChannelOutputDto>>>(
            JsonSerializerOptions.Web);
        body!.Data.Items.Count.ShouldBe(4);
        body.Data.TotalCount.ShouldBe(4);
    }

    [Fact]
    public async Task GetAdsChannels_WithPageSizeOne_Returns200WithOneItemButFullTotalCount()
    {
        await SeedAsync();

        var response = await Client.GetAsync("/ads-channels?pageIndex=0&pageSize=1");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<ApiPagedData<AdsChannelOutputDto>>>(
            JsonSerializerOptions.Web);
        body!.Data.Items.Count.ShouldBe(1);
        body.Data.TotalCount.ShouldBe(4);
    }

    [Fact]
    public async Task GetAdsChannels_WithNameContainsFilter_Returns200WithOnlyMatchingItems()
    {
        await SeedAsync();

        var response = await Client.GetAsync("/ads-channels?nameContains=Google");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<ApiPagedData<AdsChannelOutputDto>>>(
            JsonSerializerOptions.Web);
        body!.Data.TotalCount.ShouldBe(1);
        body.Data.Items.ShouldHaveSingleItem();
        body.Data.Items[0].Name.ShouldBe("Google Ads");
    }

    [Fact]
    public async Task GetAdsChannels_WithIsActiveFalseFilter_Returns200WithOnlyInactiveItems()
    {
        await SeedAsync();

        var response = await Client.GetAsync("/ads-channels?isActive=false");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<ApiPagedData<AdsChannelOutputDto>>>(
            JsonSerializerOptions.Web);
        body!.Data.TotalCount.ShouldBe(2);
        body.Data.Items.ShouldAllBe(x => !x.IsActive);
    }

    private async Task SeedAsync()
    {
        Db.AdsChannels.AddRange(
            new global::Infrastructure.Persistence.EntityFramework.AdsChannels.Entities.AdsChannel
            {
                Name = "Google Ads",
                IsActive = true,
            },
            new global::Infrastructure.Persistence.EntityFramework.AdsChannels.Entities.AdsChannel
            {
                Name = "Meta Ads",
                IsActive = true,
            },
            new global::Infrastructure.Persistence.EntityFramework.AdsChannels.Entities.AdsChannel
            {
                Name = "TikTok Ads",
                IsActive = false,
            },
            new global::Infrastructure.Persistence.EntityFramework.AdsChannels.Entities.AdsChannel
            {
                Name = "Legacy Banner Network",
                IsActive = false,
            });

        await Db.SaveChangesAsync().ConfigureAwait(false);
    }
}
