using AdsChannel.Domain.Queries;
using Infrastructure.Persistence.EntityFramework.AdsChannels;
using IntegrationTests.Infrastructure;
using NSubstitute;
using Shared.Application.Ports;
using Shared.Domain.Pagination;
using Shouldly;
using Xunit;
using AdsChannelDocument = Infrastructure.Persistence.EntityFramework.AdsChannels.Entities.AdsChannel;

namespace IntegrationTests.Infrastructure.Persistence.AdsChannels;

// These behaviors are asserted here (against the real SqlServerContainerFixture) rather than with
// EF's InMemory provider, because ordering, LIKE-based filtering, and pagination all depend on SQL
// Server's actual collation/OFFSET-FETCH semantics, which InMemory does not reproduce. Ids are always
// left for SQL Server's real IDENTITY column to assign — medpub_consecutivoP rejects explicit values
// without IDENTITY_INSERT ON, unlike EF's InMemory provider.
[Collection(IntegrationTestCollection.Name)]
public sealed class AdsChannelRepositoryTests : IntegrationTestBase
{
    public AdsChannelRepositoryTests(SqlServerContainerFixture fixture) : base(fixture) { }

    private AdsChannelRepository CreateSut() =>
        new(Db, Substitute.For<ILoggerPort<AdsChannelRepository>>());

    private static AdsChannelDocument Document(string name, bool isActive = true) =>
        new() { Name = name, IsActive = isActive };

    [Fact]
    public async Task GetAsync_WithNameContainsFilter_ReturnsOnlyMatchingItems()
    {
        Db.AdsChannels.AddRange(
            Document("Google Ads"),
            Document("Meta Ads"),
            Document("Google Analytics"));
        await Db.SaveChangesAsync();
        var sut = CreateSut();

        var result = await sut.GetAsync(new AdsChannelFilter("Google", null), new PageQuery(0, 10));

        result.IsSuccess.ShouldBeTrue();
        result.TotalCount.ShouldBe(2);
        result.Items.Select(x => x.Name).ShouldBe(["Google Ads", "Google Analytics"]);
    }

    [Fact]
    public async Task GetAsync_WithIsActiveFilter_ReturnsOnlyMatchingItems()
    {
        Db.AdsChannels.AddRange(
            Document("Google Ads", isActive: true),
            Document("Meta Ads", isActive: false));
        await Db.SaveChangesAsync();
        var sut = CreateSut();

        var result = await sut.GetAsync(new AdsChannelFilter(null, false), new PageQuery(0, 10));

        result.IsSuccess.ShouldBeTrue();
        result.TotalCount.ShouldBe(1);
        result.Items.Single().Name.ShouldBe("Meta Ads");
    }

    [Fact]
    public async Task GetAsync_WithoutFilters_ReturnsAllOrderedByNameThenId()
    {
        Db.AdsChannels.AddRange(
            Document("Beta"),
            Document("Alpha"),
            Document("Gamma"));
        await Db.SaveChangesAsync();
        var sut = CreateSut();

        var result = await sut.GetAsync(new AdsChannelFilter(null, null), new PageQuery(0, 10));

        result.IsSuccess.ShouldBeTrue();
        result.Items.Select(x => x.Name).ShouldBe(["Alpha", "Beta", "Gamma"]);
    }

    [Fact]
    public async Task GetAsync_Paginates_UsingSkipAndTake()
    {
        Db.AdsChannels.AddRange(
            Document("Alpha"),
            Document("Beta"),
            Document("Gamma"));
        await Db.SaveChangesAsync();
        var sut = CreateSut();

        var secondPage = await sut.GetAsync(new AdsChannelFilter(null, null), new PageQuery(1, 2));

        secondPage.IsSuccess.ShouldBeTrue();
        secondPage.TotalCount.ShouldBe(3);
        secondPage.Items.Select(x => x.Name).ShouldBe(["Gamma"]);
    }

    [Fact]
    public async Task ExistsByNameAsync_WhenNameExists_ReturnsTrue()
    {
        Db.AdsChannels.Add(Document("Google Ads"));
        await Db.SaveChangesAsync();
        var sut = CreateSut();

        var result = await sut.ExistsByNameAsync("Google Ads");

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeTrue();
    }

    [Fact]
    public async Task ExistsByNameAsync_WhenNameDoesNotExist_ReturnsFalse()
    {
        var sut = CreateSut();

        var result = await sut.ExistsByNameAsync("Unknown");

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeFalse();
    }

    [Fact]
    public async Task ExistsByNameAsync_WhenExcludingTheOnlyMatchingId_ReturnsFalse()
    {
        var document = Document("Google Ads");
        Db.AdsChannels.Add(document);
        await Db.SaveChangesAsync();
        var sut = CreateSut();

        var result = await sut.ExistsByNameAsync("Google Ads", excludingId: document.Id);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeFalse();
    }

    [Fact]
    public async Task ExistsByNameAsync_WhenExcludingADifferentId_StillReturnsTrue()
    {
        var document = Document("Google Ads");
        Db.AdsChannels.Add(document);
        await Db.SaveChangesAsync();
        var sut = CreateSut();

        var result = await sut.ExistsByNameAsync("Google Ads", excludingId: document.Id + 1);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeTrue();
    }
}
