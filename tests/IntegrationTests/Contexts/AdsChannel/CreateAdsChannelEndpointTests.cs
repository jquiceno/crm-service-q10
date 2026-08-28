using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AdsChannel.Application.UseCases.CreateAdsChannel;
using IntegrationTests.Infrastructure;
using Shared.Presentation.Responses;
using Shouldly;
using Xunit;

namespace IntegrationTests.Contexts.AdsChannel;

[Collection(IntegrationTestCollection.Name)]
public sealed class CreateAdsChannelEndpointTests : IntegrationTestBase
{
    public CreateAdsChannelEndpointTests(SqlServerContainerFixture fixture) : base(fixture) { }

    [Fact]
    public async Task CreateAdsChannel_WithNewName_Returns201WithCreatedResource()
    {
        var input = new CreateAdsChannelInputDto("Google Ads", true);

        var response = await Client.PostAsJsonAsync("/ads-channels", input);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<CreateAdsChannelOutputDto>>(
            JsonSerializerOptions.Web);
        body!.Data.Id.ShouldBeGreaterThan(0);
        body.Data.Name.ShouldBe("Google Ads");
        body.Data.IsActive.ShouldBeTrue();
    }

    [Fact]
    public async Task CreateAdsChannel_WithDuplicateName_Returns409()
    {
        Db.AdsChannels.Add(new global::Infrastructure.Persistence.EntityFramework.AdsChannels.Entities.AdsChannel
        {
            Name = "Meta Ads",
            IsActive = true
        });
        await Db.SaveChangesAsync();

        var input = new CreateAdsChannelInputDto("Meta Ads", true);

        var response = await Client.PostAsJsonAsync("/ads-channels", input);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        var body = await response.Content.ReadFromJsonAsync<ApiErrorResponse>(JsonSerializerOptions.Web);
        body!.Error.Type.ShouldBe("CONFLICT");
        body.Error.Code.ShouldBe("HTTP.CONFLICT");
    }

    [Fact]
    public async Task CreateAdsChannel_WithEmptyName_Returns400()
    {
        var input = new CreateAdsChannelInputDto("", true);

        var response = await Client.PostAsJsonAsync("/ads-channels", input);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadFromJsonAsync<ApiErrorResponse>(JsonSerializerOptions.Web);
        body!.Error.Type.ShouldBe("VALIDATION");
        body.Error.Code.ShouldBe("HTTP.VALIDATION");
    }
}
