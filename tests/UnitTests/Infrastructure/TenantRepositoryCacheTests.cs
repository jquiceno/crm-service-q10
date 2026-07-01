using Infrastructure.MasterAccess.Persistence.EntityFramework;
using Infrastructure.MasterAccess.Persistence.EntityFramework.Tenants;
using Infrastructure.MasterAccess.Persistence.EntityFramework.Tenants.Entities;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Shared.Application.Ports;
using Shouldly;
using Xunit;

namespace UnitTests.Infrastructure;

public sealed class TenantRepositoryCacheTests
{
    private static MasterAccessDbContext NewContext() =>
        new(new DbContextOptionsBuilder<MasterAccessDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public async Task GetByCodeAsync_StoresUnderCanonicalKey()
    {
        using var context = NewContext();
        context.Tenants.Add(new Tenant { Code = "acme", Database = "acme_db", ServerDatabase = 1 });
        await context.SaveChangesAsync();

        var cache = new JsonRoundTripCacheStore();
        var sut = new TenantRepository(context, Substitute.For<ILoggerPort<TenantRepository>>(), cache);

        await sut.GetByCodeAsync("acme");

        cache.Keys.ShouldContain("ctx:masteraccess:v1:tenant:acme");
    }

    [Fact]
    public async Task GetByCodeAsync_SecondCall_ServesReconstructedAggregateFromCache()
    {
        using var context = NewContext();
        context.Tenants.Add(new Tenant { Code = "acme", Database = "acme_db", ServerDatabase = 7 });
        await context.SaveChangesAsync();

        var cache = new JsonRoundTripCacheStore();
        var sut = new TenantRepository(context, Substitute.For<ILoggerPort<TenantRepository>>(), cache);

        var first = await sut.GetByCodeAsync("acme");
        first.IsSuccess.ShouldBeTrue();

        // Remove the row so a DB re-query would find nothing; a successful result now proves a cache hit
        // AND that the cached snapshot round-tripped through System.Text.Json.
        context.Tenants.RemoveRange(context.Tenants);
        await context.SaveChangesAsync();

        var second = await sut.GetByCodeAsync("acme");

        second.IsSuccess.ShouldBeTrue();
        second.Value.Code.ShouldBe("acme");
        second.Value.Database.ShouldBe("acme_db");
        second.Value.ServerDatabase.ShouldBe(7);
    }
}
