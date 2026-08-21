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
        // maps it, so EnsureCreatedAsync never creates it in the Testcontainers database (it only
        // creates tables for entities the DbContext knows about) — a plain INSERT against it fails
        // with "Invalid object name", not a constraint violation. A minimal stand-in with just the FK
        // relationship that matters for this test is created here so the real FK-conflict -> 409 path
        // (D4) is exercised end-to-end, without depending on the legacy table's full, unverified shape
        // (see Discovery GAP-1 for tbl_opo_oportunidades).
        await Db.Database.ExecuteSqlRawAsync(
            """
            IF OBJECT_ID('tbl_opo_oportunidades', 'U') IS NULL
            CREATE TABLE tbl_opo_oportunidades (
                opo_consecutivoP INT IDENTITY PRIMARY KEY,
                opo_medpub_consecutivoP INT NOT NULL
                    REFERENCES tbl_opo_medios_publicitarios (medpub_consecutivoP)
            );
            """);

        await Db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO tbl_opo_oportunidades (opo_medpub_consecutivoP) VALUES ({adsChannel.Id})");

        var response = await Client.DeleteAsync($"/ads-channels/{adsChannel.Id}");

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }
}
