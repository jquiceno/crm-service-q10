using BusinessStatus.Domain.Enums;
using BusinessStatus.Domain.Queries;
using Infrastructure.Persistence.EntityFramework;
using Infrastructure.Persistence.EntityFramework.BusinessStatuses;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Shared.Application.Ports;
using Shared.Domain.Pagination;
using Shared.Results.Errors;
using Shouldly;
using Xunit;
using Entities = Infrastructure.Persistence.EntityFramework.BusinessStatuses.Entities;

namespace UnitTests.Infrastructure.Persistence.BusinessStatuses;

/// <summary>
/// The L2 cache-aside of the catalogue listing. It runs against <see cref="JsonRoundTripCacheStore"/>
/// rather than a substitute so the snapshot is really (de)serialized: a cached type that
/// System.Text.Json cannot rebuild would degrade to a silent 0 % hit rate in production, and here it
/// fails the test instead.
/// </summary>
public sealed class BusinessStatusRepositoryCacheTests
{
    private const string TenantCode = "ACME";

    private readonly ILoggerPort<BusinessStatusRepository> _logger =
        Substitute.For<ILoggerPort<BusinessStatusRepository>>();

    private readonly JsonRoundTripCacheStore _cacheStore = new();
    private readonly ITenantCodeProvider _tenantCodeProvider = Substitute.For<ITenantCodeProvider>();

    private static readonly PageQuery FirstPage = new(pageIndex: 0, pageSize: 20);

    private static BusinessStatusFilter NoFilter =>
        new(Name: null, IsActive: null, BusinessStatusKind.All);

    public BusinessStatusRepositoryCacheTests() => _tenantCodeProvider.Current.Returns(TenantCode);

    private static ApplicationDbContext CreateContext(string databaseName) =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(databaseName).Options);

    private BusinessStatusRepository NewRepository(ApplicationDbContext context) =>
        new(context, _logger, _cacheStore, _tenantCodeProvider);

    private static async Task SeedAsync(string databaseName, params Entities.BusinessStatus[] rows)
    {
        using var context = CreateContext(databaseName);
        context.BusinessStatuses.AddRange(rows);
        await context.SaveChangesAsync().ConfigureAwait(false);
    }

    private static async Task DeleteEverythingAsync(string databaseName)
    {
        using var context = CreateContext(databaseName);
        context.BusinessStatuses.RemoveRange(context.BusinessStatuses);
        await context.SaveChangesAsync().ConfigureAwait(false);
    }

    private static Entities.BusinessStatus Row(int id, decimal? percentage = 50m, string? name = "Negotiation") =>
        new() { Id = id, Name = name, IsActive = true, Percentage = percentage, Color = "49ff7c" };

    [Fact]
    public async Task GetAsync_OnAMiss_PopulatesTheCacheUnderTheTenantPartitionedKey()
    {
        var database = nameof(GetAsync_OnAMiss_PopulatesTheCacheUnderTheTenantPartitionedKey);
        await SeedAsync(database, Row(7));
        using var context = CreateContext(database);

        await NewRepository(context).GetAsync(NoFilter, FirstPage);

        var key = _cacheStore.Keys.ShouldHaveSingleItem();
        key.ShouldStartWith($"ctx:businessstatus:v1:t:{TenantCode}:list:");
    }

    [Fact]
    public async Task GetAsync_OnASecondIdenticalCall_IsServedFromTheCache()
    {
        var database = nameof(GetAsync_OnASecondIdenticalCall_IsServedFromTheCache);
        await SeedAsync(database, Row(7));

        using (var first = CreateContext(database))
            await NewRepository(first).GetAsync(NoFilter, FirstPage);

        // The row disappears from the store: whatever the second call returns cannot have come from
        // the database.
        await DeleteEverythingAsync(database);

        using var second = CreateContext(database);
        var result = await NewRepository(second).GetAsync(NoFilter, FirstPage);

        result.IsSuccess.ShouldBeTrue();
        result.TotalCount.ShouldBe(1);
        var item = result.Items.ShouldHaveSingleItem();
        item.Id.ShouldBe(7);
        item.Name.ShouldBe("Negotiation");
        item.Percentage.ShouldBe(50);
        item.Color!.Value.ShouldBe("49ff7c");
        item.IsActive.ShouldBeTrue();
    }

    [Fact]
    public async Task GetAsync_WithADifferentFilter_IsAMissAndKeepsBothEntries()
    {
        var database = nameof(GetAsync_WithADifferentFilter_IsAMissAndKeepsBothEntries);
        await SeedAsync(database, Row(7));
        using var context = CreateContext(database);
        var repository = NewRepository(context);

        await repository.GetAsync(NoFilter, FirstPage);
        await repository.GetAsync(new BusinessStatusFilter("Nego", IsActive: null, BusinessStatusKind.All), FirstPage);

        _cacheStore.Keys.Count.ShouldBe(2, "a different filter is a different entry, never a false hit");
    }

    [Fact]
    public async Task GetAsync_WithADifferentPage_IsAMissAndKeepsBothEntries()
    {
        var database = nameof(GetAsync_WithADifferentPage_IsAMissAndKeepsBothEntries);
        await SeedAsync(database, Row(7));
        using var context = CreateContext(database);
        var repository = NewRepository(context);

        await repository.GetAsync(NoFilter, FirstPage);
        await repository.GetAsync(NoFilter, new PageQuery(pageIndex: 1, pageSize: 20));

        _cacheStore.Keys.Count.ShouldBe(2, "the page is part of the key");
    }

    [Fact]
    public async Task GetAsync_WhenTheQueryFails_CachesNothing()
    {
        var context = CreateContext(nameof(GetAsync_WhenTheQueryFails_CachesNothing));
        var repository = NewRepository(context);
        await context.DisposeAsync();

        var result = await repository.GetAsync(NoFilter, FirstPage);

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Internal);
        _cacheStore.Keys.ShouldBeEmpty("only successes are cached");
    }

    [Fact]
    public async Task GetAsync_WithoutAResolvedTenant_SkipsTheCacheAltogether()
    {
        var database = nameof(GetAsync_WithoutAResolvedTenant_SkipsTheCacheAltogether);
        _tenantCodeProvider.Current.Returns((string?)null);
        await SeedAsync(database, Row(7));
        using var context = CreateContext(database);

        var result = await NewRepository(context).GetAsync(NoFilter, FirstPage);

        result.IsSuccess.ShouldBeTrue("the listing still answers from the database");
        _cacheStore.Keys.ShouldBeEmpty("with no tenant there is nothing to partition by");
    }

    [Fact]
    public async Task GetAsync_WithATenantCodeThatCannotBeAKeySegment_DegradesToNoCache()
    {
        // CacheKey refuses a segment containing ':'. That must cost the cache, never the listing.
        var database = nameof(GetAsync_WithATenantCodeThatCannotBeAKeySegment_DegradesToNoCache);
        _tenantCodeProvider.Current.Returns("bad:code");
        await SeedAsync(database, Row(7));
        using var context = CreateContext(database);

        var result = await NewRepository(context).GetAsync(NoFilter, FirstPage);

        result.IsSuccess.ShouldBeTrue("a key that cannot be built is not a reason to fail the query");
        result.Items.ShouldHaveSingleItem();
        _cacheStore.Keys.ShouldBeEmpty();
        _logger.ReceivedWithAnyArgs(1).Warning(string.Empty);
    }

    [Fact]
    public async Task GetAsync_PartitionsTheEntryByTenant()
    {
        var database = nameof(GetAsync_PartitionsTheEntryByTenant);
        await SeedAsync(database, Row(7));

        using (var first = CreateContext(database))
            await NewRepository(first).GetAsync(NoFilter, FirstPage);

        _tenantCodeProvider.Current.Returns("OTHER");

        using var second = CreateContext(database);
        await NewRepository(second).GetAsync(NoFilter, FirstPage);

        _cacheStore.Keys.Count.ShouldBe(2, "two tenants never share an entry");
        _cacheStore.Keys.ShouldContain(k => k.Contains($":t:{TenantCode}:", StringComparison.Ordinal));
        _cacheStore.Keys.ShouldContain(k => k.Contains(":t:OTHER:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task GetAllAsync_SharesTheEntryOfTheUnfilteredListing()
    {
        var database = nameof(GetAllAsync_SharesTheEntryOfTheUnfilteredListing);
        await SeedAsync(database, Row(7));
        using var context = CreateContext(database);
        var repository = NewRepository(context);

        await repository.GetAsync(NoFilter, FirstPage);
        await repository.GetAllAsync(FirstPage);

        _cacheStore.Keys.ShouldHaveSingleItem(
            "GetAllAsync delegates with an empty filter, so it is the same key");
    }
}
