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
// Server's actual collation/OFFSET-FETCH semantics, which InMemory does not reproduce.
[Collection(IntegrationTestCollection.Name)]
public sealed class AdsChannelRepositoryTests : IntegrationTestBase
{
    public AdsChannelRepositoryTests(SqlServerContainerFixture fixture) : base(fixture) { }

    private AdsChannelRepository CreateSut() =>
        new(Db, Substitute.For<ILoggerPort<AdsChannelRepository>>());

    private static AdsChannelDocument Document(int id, string name, bool isActive = true) =>
        new() { Id = id, Name = name, IsActive = isActive };

    [Fact]
    public async Task GetAsync_WithNameContainsFilter_ReturnsOnlyMatchingItems()
    {
        Db.AdsChannels.AddRange(
            Document(1, "Google Ads"),
            Document(2, "Meta Ads"),
            Document(3, "Google Analytics"));
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
            Document(1, "Google Ads", isActive: true),
            Document(2, "Meta Ads", isActive: false));
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
            Document(2, "Beta"),
            Document(1, "Alpha"),
            Document(3, "Gamma"));
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
            Document(1, "Alpha"),
            Document(2, "Beta"),
            Document(3, "Gamma"));
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
        Db.AdsChannels.Add(Document(1, "Google Ads"));
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
        Db.AdsChannels.Add(Document(1, "Google Ads"));
        await Db.SaveChangesAsync();
        var sut = CreateSut();

        var result = await sut.ExistsByNameAsync("Google Ads", excludingId: 1);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeFalse();
    }

    [Fact]
    public async Task ExistsByNameAsync_WhenExcludingADifferentId_StillReturnsTrue()
    {
        Db.AdsChannels.Add(Document(1, "Google Ads"));
        await Db.SaveChangesAsync();
        var sut = CreateSut();

        var result = await sut.ExistsByNameAsync("Google Ads", excludingId: 2);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeTrue();
    }
}
