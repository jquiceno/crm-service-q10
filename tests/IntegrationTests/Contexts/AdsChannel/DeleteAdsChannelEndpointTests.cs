using System.Net;
using IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace IntegrationTests.Contexts.AdsChannel;

[Collection(IntegrationTestCollection.Name)]
public sealed class DeleteAdsChannelEndpointTests : IntegrationTestBase
{
    public DeleteAdsChannelEndpointTests(SqlServerContainerFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Delete_ExistingUnreferencedId_Returns204_AndRemovesTheRow()
    {
        var adsChannel = new global::Infrastructure.Persistence.EntityFramework.AdsChannels.Entities.AdsChannel
        {
            Name = "Radio",
            IsActive = true,
        };
        Db.AdsChannels.Add(adsChannel);
        await Db.SaveChangesAsync();

        var response = await Client.DeleteAsync($"/ads-channels/{adsChannel.Id}");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var content = await response.Content.ReadAsByteArrayAsync();
        content.ShouldBeEmpty();

        var stillExists = await Db.AdsChannels.AsNoTracking().AnyAsync(x => x.Id == adsChannel.Id);
        stillExists.ShouldBeFalse();
    }

    [Fact]
    public async Task Delete_NonExistentId_Returns404()
    {
        const int nonExistentId = int.MaxValue;

        var response = await Client.DeleteAsync($"/ads-channels/{nonExistentId}");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_IdReferencedByAnotherTable_Returns409()
    {
        var adsChannel = new global::Infrastructure.Persistence.EntityFramework.AdsChannels.Entities.AdsChannel
        {
            Name = "TV",
            IsActive = true,
        };
        Db.AdsChannels.Add(adsChannel);
        await Db.SaveChangesAsync();

        // tbl_opo_oportunidades is a legacy table outside this bounded context: no EF entity/DbSet
        // exists for it here, so the referencing row is seeded with raw SQL against the real column
        // name (opo_medpub_consecutivoP). If this table has other NOT NULL columns without defaults,
        // this insert may need to be adjusted once the real table shape can be inspected — see report.
        await Db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO tbl_opo_oportunidades (opo_medpub_consecutivoP) VALUES ({adsChannel.Id})");

        var response = await Client.DeleteAsync($"/ads-channels/{adsChannel.Id}");

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }
}
