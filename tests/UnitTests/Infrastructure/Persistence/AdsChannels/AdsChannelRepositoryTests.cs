using AdsChannel.Domain.Aggregates;
using AdsChannel.Domain.Queries;
using Infrastructure.Persistence.EntityFramework;
using Infrastructure.Persistence.EntityFramework.AdsChannels;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Shared.Application.Ports;
using Shared.Domain.Pagination;
using Shared.Results.Errors;
using Shouldly;
using Xunit;
using AdsChannelDocument = Infrastructure.Persistence.EntityFramework.AdsChannels.Entities.AdsChannel;

namespace UnitTests.Infrastructure.Persistence.AdsChannels;

public sealed class AdsChannelRepositoryTests
{
    private static ApplicationDbContext CreateContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new ApplicationDbContext(options);
    }

    private static AdsChannelDocument Document(int id, string name, bool isActive = true) =>
        new() { Id = id, Name = name, IsActive = isActive };

    // ── GetByIdAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GetByIdAsync_WhenExists_ReturnsMappedAggregate()
    {
        using var context = CreateContext(nameof(GetByIdAsync_WhenExists_ReturnsMappedAggregate));
        context.AdsChannels.Add(Document(1, "Google Ads"));
        await context.SaveChangesAsync();
        var sut = new AdsChannelRepository(context, Substitute.For<ILoggerPort<AdsChannelRepository>>());

        var result = await sut.GetByIdAsync(1);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Id.ShouldBe(1);
        result.Value.Name.ShouldBe("Google Ads");
    }

    [Fact]
    public async Task GetByIdAsync_WhenNotFound_ReturnsNotFoundError()
    {
        using var context = CreateContext(nameof(GetByIdAsync_WhenNotFound_ReturnsNotFoundError));
        var sut = new AdsChannelRepository(context, Substitute.For<ILoggerPort<AdsChannelRepository>>());

        var result = await sut.GetByIdAsync(404);

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.NotFound);
        result.Error.Message.ShouldContain("404");
    }

    [Fact]
    public async Task GetByIdAsync_WhenExceptionThrown_ReturnsInternalErrorAndLogs()
    {
        var context = CreateContext(nameof(GetByIdAsync_WhenExceptionThrown_ReturnsInternalErrorAndLogs));
        var logger = Substitute.For<ILoggerPort<AdsChannelRepository>>();
        var sut = new AdsChannelRepository(context, logger);
        await context.DisposeAsync();

        var result = await sut.GetByIdAsync(1);

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Internal);
        logger.Received(1).Error(Arg.Is<Exception?>(e => e != null), Arg.Any<string>(), Arg.Any<object[]>());
    }

    // ── ExistsAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task ExistsAsync_WhenTheAggregateExists_ReturnsTrue()
    {
        using var context = CreateContext(nameof(ExistsAsync_WhenTheAggregateExists_ReturnsTrue));
        context.AdsChannels.Add(Document(1, "Google Ads"));
        await context.SaveChangesAsync();
        var sut = new AdsChannelRepository(context, Substitute.For<ILoggerPort<AdsChannelRepository>>());

        var result = await sut.ExistsAsync(1);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeTrue();
    }

    [Fact]
    public async Task ExistsAsync_WithAnUnknownId_SucceedsWithFalse()
    {
        using var context = CreateContext(nameof(ExistsAsync_WithAnUnknownId_SucceedsWithFalse));
        var sut = new AdsChannelRepository(context, Substitute.For<ILoggerPort<AdsChannelRepository>>());

        var result = await sut.ExistsAsync(404);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeFalse();
    }

    [Fact]
    public async Task ExistsAsync_WhenExceptionThrown_ReturnsInternalErrorAndLogs()
    {
        var context = CreateContext(nameof(ExistsAsync_WhenExceptionThrown_ReturnsInternalErrorAndLogs));
        var logger = Substitute.For<ILoggerPort<AdsChannelRepository>>();
        var sut = new AdsChannelRepository(context, logger);
        await context.DisposeAsync();

        var result = await sut.ExistsAsync(1);

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Internal);
        logger.Received(1).Error(Arg.Is<Exception?>(e => e != null), Arg.Any<string>(), Arg.Any<object[]>());
    }

    // ── GetAllAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAllAsync_DelegatesToGetAsyncWithAnEmptyFilter()
    {
        using var context = CreateContext(nameof(GetAllAsync_DelegatesToGetAsyncWithAnEmptyFilter));
        context.AdsChannels.AddRange(
            Document(1, "Google Ads", isActive: true),
            Document(2, "Meta Ads", isActive: false));
        await context.SaveChangesAsync();
        var sut = new AdsChannelRepository(context, Substitute.For<ILoggerPort<AdsChannelRepository>>());

        var result = await sut.GetAllAsync(new PageQuery(0, 10));

        result.IsSuccess.ShouldBeTrue();
        result.TotalCount.ShouldBe(2);
    }

    // ── ExistsByNameAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task ExistsByNameAsync_WhenNameExists_ReturnsTrue()
    {
        using var context = CreateContext(nameof(ExistsByNameAsync_WhenNameExists_ReturnsTrue));
        context.AdsChannels.Add(Document(1, "Google Ads"));
        await context.SaveChangesAsync();
        var sut = new AdsChannelRepository(context, Substitute.For<ILoggerPort<AdsChannelRepository>>());

        var result = await sut.ExistsByNameAsync("Google Ads");

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeTrue();
    }

    [Fact]
    public async Task ExistsByNameAsync_WhenNameDoesNotExist_ReturnsFalse()
    {
        using var context = CreateContext(nameof(ExistsByNameAsync_WhenNameDoesNotExist_ReturnsFalse));
        var sut = new AdsChannelRepository(context, Substitute.For<ILoggerPort<AdsChannelRepository>>());

        var result = await sut.ExistsByNameAsync("Unknown");

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeFalse();
    }

    [Fact]
    public async Task ExistsByNameAsync_WhenExcludingTheOnlyMatchingId_ReturnsFalse()
    {
        using var context = CreateContext(nameof(ExistsByNameAsync_WhenExcludingTheOnlyMatchingId_ReturnsFalse));
        context.AdsChannels.Add(Document(1, "Google Ads"));
        await context.SaveChangesAsync();
        var sut = new AdsChannelRepository(context, Substitute.For<ILoggerPort<AdsChannelRepository>>());

        var result = await sut.ExistsByNameAsync("Google Ads", excludingId: 1);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeFalse();
    }

    [Fact]
    public async Task ExistsByNameAsync_WhenExcludingADifferentId_StillReturnsTrue()
    {
        using var context = CreateContext(nameof(ExistsByNameAsync_WhenExcludingADifferentId_StillReturnsTrue));
        context.AdsChannels.Add(Document(1, "Google Ads"));
        await context.SaveChangesAsync();
        var sut = new AdsChannelRepository(context, Substitute.For<ILoggerPort<AdsChannelRepository>>());

        var result = await sut.ExistsByNameAsync("Google Ads", excludingId: 2);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeTrue();
    }

    [Fact]
    public async Task ExistsByNameAsync_WhenExceptionThrown_ReturnsInternalErrorAndLogs()
    {
        var context = CreateContext(nameof(ExistsByNameAsync_WhenExceptionThrown_ReturnsInternalErrorAndLogs));
        var logger = Substitute.For<ILoggerPort<AdsChannelRepository>>();
        var sut = new AdsChannelRepository(context, logger);
        await context.DisposeAsync();

        var result = await sut.ExistsByNameAsync("Google Ads");

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Internal);
        logger.Received(1).Error(Arg.Is<Exception?>(e => e != null), Arg.Any<string>(), Arg.Any<object[]>());
    }

    // ── GetAsync ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAsync_WithNameContainsFilter_ReturnsOnlyMatchingItems()
    {
        using var context = CreateContext(nameof(GetAsync_WithNameContainsFilter_ReturnsOnlyMatchingItems));
        context.AdsChannels.AddRange(
            Document(1, "Google Ads"),
            Document(2, "Meta Ads"),
            Document(3, "Google Analytics"));
        await context.SaveChangesAsync();
        var sut = new AdsChannelRepository(context, Substitute.For<ILoggerPort<AdsChannelRepository>>());

        var result = await sut.GetAsync(new AdsChannelFilter("Google", null), new PageQuery(0, 10));

        result.IsSuccess.ShouldBeTrue();
        result.TotalCount.ShouldBe(2);
        result.Items.Select(x => x.Name).ShouldBe(["Google Ads", "Google Analytics"]);
    }

    [Fact]
    public async Task GetAsync_WithIsActiveFilter_ReturnsOnlyMatchingItems()
    {
        using var context = CreateContext(nameof(GetAsync_WithIsActiveFilter_ReturnsOnlyMatchingItems));
        context.AdsChannels.AddRange(
            Document(1, "Google Ads", isActive: true),
            Document(2, "Meta Ads", isActive: false));
        await context.SaveChangesAsync();
        var sut = new AdsChannelRepository(context, Substitute.For<ILoggerPort<AdsChannelRepository>>());

        var result = await sut.GetAsync(new AdsChannelFilter(null, false), new PageQuery(0, 10));

        result.IsSuccess.ShouldBeTrue();
        result.TotalCount.ShouldBe(1);
        result.Items.Single().Name.ShouldBe("Meta Ads");
    }

    [Fact]
    public async Task GetAsync_WithoutFilters_ReturnsAllOrderedByNameThenId()
    {
        using var context = CreateContext(nameof(GetAsync_WithoutFilters_ReturnsAllOrderedByNameThenId));
        context.AdsChannels.AddRange(
            Document(2, "Beta"),
            Document(1, "Alpha"),
            Document(3, "Gamma"));
        await context.SaveChangesAsync();
        var sut = new AdsChannelRepository(context, Substitute.For<ILoggerPort<AdsChannelRepository>>());

        var result = await sut.GetAsync(new AdsChannelFilter(null, null), new PageQuery(0, 10));

        result.IsSuccess.ShouldBeTrue();
        result.Items.Select(x => x.Name).ShouldBe(["Alpha", "Beta", "Gamma"]);
    }

    [Fact]
    public async Task GetAsync_Paginates_UsingSkipAndTake()
    {
        using var context = CreateContext(nameof(GetAsync_Paginates_UsingSkipAndTake));
        context.AdsChannels.AddRange(
            Document(1, "Alpha"),
            Document(2, "Beta"),
            Document(3, "Gamma"));
        await context.SaveChangesAsync();
        var sut = new AdsChannelRepository(context, Substitute.For<ILoggerPort<AdsChannelRepository>>());

        var secondPage = await sut.GetAsync(new AdsChannelFilter(null, null), new PageQuery(1, 2));

        secondPage.IsSuccess.ShouldBeTrue();
        secondPage.TotalCount.ShouldBe(3);
        secondPage.Items.Select(x => x.Name).ShouldBe(["Gamma"]);
    }

    [Fact]
    public async Task GetAsync_WhenExceptionThrown_ReturnsInternalErrorAndLogs()
    {
        var context = CreateContext(nameof(GetAsync_WhenExceptionThrown_ReturnsInternalErrorAndLogs));
        var logger = Substitute.For<ILoggerPort<AdsChannelRepository>>();
        var sut = new AdsChannelRepository(context, logger);
        await context.DisposeAsync();

        var result = await sut.GetAsync(new AdsChannelFilter(null, null), new PageQuery(0, 10));

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Internal);
        logger.Received(1).Error(Arg.Is<Exception?>(e => e != null), Arg.Any<string>(), Arg.Any<object[]>());
    }

    // ── AddAsync ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task AddAsync_WhenValid_TracksTheMappedDocument()
    {
        using var context = CreateContext(nameof(AddAsync_WhenValid_TracksTheMappedDocument));
        var sut = new AdsChannelRepository(context, Substitute.For<ILoggerPort<AdsChannelRepository>>());
        var aggregate = AdsChannelAggregate.Create(new CreateAdsChannelArgs("Google Ads")).Value;

        var result = await sut.AddAsync(aggregate);
        await context.SaveChangesAsync();

        result.IsSuccess.ShouldBeTrue();
        (await context.AdsChannels.CountAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task AddAsync_WhenExceptionThrown_ReturnsInternalErrorAndLogs()
    {
        var context = CreateContext(nameof(AddAsync_WhenExceptionThrown_ReturnsInternalErrorAndLogs));
        var logger = Substitute.For<ILoggerPort<AdsChannelRepository>>();
        var sut = new AdsChannelRepository(context, logger);
        await context.DisposeAsync();
        var aggregate = AdsChannelAggregate.Create(new CreateAdsChannelArgs("Google Ads")).Value;

        var result = await sut.AddAsync(aggregate);

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Internal);
        logger.Received(1).Error(Arg.Is<Exception?>(e => e != null), Arg.Any<string>(), Arg.Any<object[]>());
    }

    // ── Update ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Update_WhenValid_PersistsChangesOnSaveChanges()
    {
        const string dbName = nameof(Update_WhenValid_PersistsChangesOnSaveChanges);
        using (var seedCtx = CreateContext(dbName))
        {
            seedCtx.AdsChannels.Add(Document(1, "Old name"));
            await seedCtx.SaveChangesAsync();
        }

        using var context = CreateContext(dbName);
        var sut = new AdsChannelRepository(context, Substitute.For<ILoggerPort<AdsChannelRepository>>());

        var result = sut.Update(AdsChannelAggregate.Reconstruct(1, "New name", false));
        await context.SaveChangesAsync();

        result.IsSuccess.ShouldBeTrue();
        using var verifyCtx = CreateContext(dbName);
        var updated = await verifyCtx.AdsChannels.SingleAsync();
        updated.Name.ShouldBe("New name");
        updated.IsActive.ShouldBe(false);
    }

    // ── RemoveAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task RemoveAsync_WhenExists_MarksItForDeletion()
    {
        using var context = CreateContext(nameof(RemoveAsync_WhenExists_MarksItForDeletion));
        context.AdsChannels.Add(Document(1, "Google Ads"));
        await context.SaveChangesAsync();
        var sut = new AdsChannelRepository(context, Substitute.For<ILoggerPort<AdsChannelRepository>>());

        var result = await sut.RemoveAsync(1);
        await context.SaveChangesAsync();

        result.IsSuccess.ShouldBeTrue();
        (await context.AdsChannels.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task RemoveAsync_WithAnUnknownId_ReturnsNotFoundError()
    {
        using var context = CreateContext(nameof(RemoveAsync_WithAnUnknownId_ReturnsNotFoundError));
        var sut = new AdsChannelRepository(context, Substitute.For<ILoggerPort<AdsChannelRepository>>());

        var result = await sut.RemoveAsync(404);

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.NotFound);
    }

    [Fact]
    public async Task RemoveAsync_WhenExceptionThrown_ReturnsInternalErrorAndLogs()
    {
        var context = CreateContext(nameof(RemoveAsync_WhenExceptionThrown_ReturnsInternalErrorAndLogs));
        var logger = Substitute.For<ILoggerPort<AdsChannelRepository>>();
        var sut = new AdsChannelRepository(context, logger);
        await context.DisposeAsync();

        var result = await sut.RemoveAsync(1);

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Internal);
        logger.Received(1).Error(Arg.Is<Exception?>(e => e != null), Arg.Any<string>(), Arg.Any<object[]>());
    }

    // ── CreateAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_WhenValid_PersistsAndReturnsAggregateWithGeneratedId()
    {
        using var context = CreateContext(nameof(CreateAsync_WhenValid_PersistsAndReturnsAggregateWithGeneratedId));
        var sut = new AdsChannelRepository(context, Substitute.For<ILoggerPort<AdsChannelRepository>>());
        var aggregate = AdsChannelAggregate.Create(new CreateAdsChannelArgs("Google Ads")).Value;

        var result = await sut.CreateAsync(aggregate);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Id.ShouldNotBe(0);
        result.Value.Name.ShouldBe("Google Ads");
        (await context.AdsChannels.CountAsync()).ShouldBe(1);
    }

    // The in-memory provider auto-generates the key only when the mapped document's Id is still the
    // CLR default (0). Reusing an existing, explicit Id makes SaveChangesAsync raise a DbUpdateException
    // whose inner exception is not a SqlException (SqlException has no public constructor to fabricate
    // in a unit test — see SqlServerErrorClassifierTests) — enough to exercise the catch block's
    // fallback-to-persistence-failure path, even though the true "duplicate name" branch is untestable here.
    [Fact]
    public async Task CreateAsync_WhenDbUpdateExceptionIsNotASqlServerError_ReturnsPersistenceFailure()
    {
        using var context = CreateContext(nameof(CreateAsync_WhenDbUpdateExceptionIsNotASqlServerError_ReturnsPersistenceFailure));
        context.AdsChannels.Add(Document(1, "Google Ads"));
        await context.SaveChangesAsync();
        var logger = Substitute.For<ILoggerPort<AdsChannelRepository>>();
        var sut = new AdsChannelRepository(context, logger);
        var duplicateIdAggregate = AdsChannelAggregate.Reconstruct(1, "Duplicate", true);

        var result = await sut.CreateAsync(duplicateIdAggregate);

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Internal);
        result.Error.Origin.ShouldBe(nameof(AdsChannelRepository));
        logger.Received(1).Error(Arg.Is<Exception?>(e => e != null), Arg.Any<string>(), Arg.Any<object[]>());
    }

    [Fact]
    public async Task CreateAsync_WhenExceptionThrown_ReturnsInternalErrorAndLogs()
    {
        var context = CreateContext(nameof(CreateAsync_WhenExceptionThrown_ReturnsInternalErrorAndLogs));
        var logger = Substitute.For<ILoggerPort<AdsChannelRepository>>();
        var sut = new AdsChannelRepository(context, logger);
        await context.DisposeAsync();
        var aggregate = AdsChannelAggregate.Create(new CreateAdsChannelArgs("Google Ads")).Value;

        var result = await sut.CreateAsync(aggregate);

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Internal);
        logger.Received(1).Error(Arg.Is<Exception?>(e => e != null), Arg.Any<string>(), Arg.Any<object[]>());
    }
}
