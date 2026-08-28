using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AdsChannel.Application.UseCases.UpdateAdsChannel;
using IntegrationTests.Infrastructure;
using Shared.Presentation.Responses;
using Shouldly;
using Xunit;

namespace IntegrationTests.Contexts.AdsChannel;

[Collection(IntegrationTestCollection.Name)]
public sealed class UpdateAdsChannelEndpointTests : IntegrationTestBase
{
    public UpdateAdsChannelEndpointTests(SqlServerContainerFixture fixture) : base(fixture) { }

    [Fact]
    public async Task UpdateAdsChannel_WithValidInput_Returns200WithUpdatedBody()
    {
        var channel = new global::Infrastructure.Persistence.EntityFramework.AdsChannels.Entities.AdsChannel
        {
            Name = "Google Ads",
            IsActive = true,
        };
        Db.AdsChannels.Add(channel);
        await Db.SaveChangesAsync();

        var input = new UpdateAdsChannelInputDto("Meta Ads", false);
        var response = await Client.PutAsJsonAsync($"/ads-channels/{channel.Id}", input);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<UpdateAdsChannelOutputDto>>(
            JsonSerializerOptions.Web);
        body!.Data.Id.ShouldBe(channel.Id);
        body.Data.Name.ShouldBe("Meta Ads");
        body.Data.IsActive.ShouldBeFalse();
    }

    [Fact]
    public async Task UpdateAdsChannel_WithNonexistentId_Returns404()
    {
        var input = new UpdateAdsChannelInputDto("Meta Ads", true);

        var response = await Client.PutAsJsonAsync("/ads-channels/999999", input);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var body = await response.Content.ReadFromJsonAsync<ApiErrorResponse>(JsonSerializerOptions.Web);
        body!.Error.Type.ShouldBe("NOT_FOUND");
        body.Error.Code.ShouldBe("HTTP.NOT_FOUND");
        body.StatusCode.ShouldBe(404);
    }

    [Fact]
    public async Task UpdateAdsChannel_WithNameDuplicatingAnotherRecord_Returns409()
    {
        var existing = new global::Infrastructure.Persistence.EntityFramework.AdsChannels.Entities.AdsChannel
        {
            Name = "Meta Ads",
            IsActive = true,
        };
        var toUpdate = new global::Infrastructure.Persistence.EntityFramework.AdsChannels.Entities.AdsChannel
        {
            Name = "Google Ads",
            IsActive = true,
        };
        Db.AdsChannels.AddRange(existing, toUpdate);
        await Db.SaveChangesAsync();

        var input = new UpdateAdsChannelInputDto(existing.Name, true);
        var response = await Client.PutAsJsonAsync($"/ads-channels/{toUpdate.Id}", input);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        var body = await response.Content.ReadFromJsonAsync<ApiErrorResponse>(JsonSerializerOptions.Web);
        body!.Error.Type.ShouldBe("CONFLICT");
        body.Error.Code.ShouldBe("HTTP.CONFLICT");
        body.StatusCode.ShouldBe(409);
    }

    [Fact]
    public async Task UpdateAdsChannel_WithEmptyName_Returns400()
    {
        var channel = new global::Infrastructure.Persistence.EntityFramework.AdsChannels.Entities.AdsChannel
        {
            Name = "Google Ads",
            IsActive = true,
        };
        Db.AdsChannels.Add(channel);
        await Db.SaveChangesAsync();

        var input = new UpdateAdsChannelInputDto("", true);
        var response = await Client.PutAsJsonAsync($"/ads-channels/{channel.Id}", input);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadFromJsonAsync<ApiErrorResponse>(JsonSerializerOptions.Web);
        body!.Error.Type.ShouldBe("VALIDATION");
        body.Error.Code.ShouldBe("HTTP.VALIDATION");
        body.StatusCode.ShouldBe(400);
    }
}
