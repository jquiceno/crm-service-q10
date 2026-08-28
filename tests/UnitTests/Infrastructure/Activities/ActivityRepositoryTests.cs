using Activities.Domain.Aggregates;
using Activities.Domain.Enums;
using Activities.Domain.Queries;
using Activities.Domain.ValueObjects;
using Infrastructure.Persistence.EntityFramework;
using Infrastructure.Persistence.EntityFramework.Activities;
using Infrastructure.Persistence.EntityFramework.Activities.Entities;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Shared.Application.Ports;
using Shared.Domain.Pagination;
using Shared.Results.Errors;
using Shouldly;
using Xunit;

namespace UnitTests.Infrastructure.Activities;

public sealed class ActivityRepositoryTests
{
    private static PersonCode Advisor => PersonCode.Create("advisor-01").Value;
    private static PersonCode Creator => PersonCode.Create("creator-01").Value;

    private static ApplicationDbContext CreateContext(string dbName) =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(dbName).Options);

    private static ILoggerPort<ActivityRepository> Logger() =>
        Substitute.For<ILoggerPort<ActivityRepository>>();

    private static Activity NewEntity(int id, int dealId, int? opportunityId = null) => new()
    {
        Id = id,
        DealId = dealId,
        OpportunityId = opportunityId,
        Type = "1",
        CreatedAt = DateTime.UtcNow,
        CreatedById = "creator-01",
    };

    private static ActivityAggregate NewActivity(int dealId) =>
        ActivityAggregate.Schedule(
            dealId, null, ActivityType.Call, Description.Create("call back").Value,
            DateTime.UtcNow.AddDays(1), Advisor, Creator).Value;

    // ── GetByIdAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GetByIdAsync_WhenExists_ReturnsTheMappedActivity()
    {
        using var context = CreateContext(nameof(GetByIdAsync_WhenExists_ReturnsTheMappedActivity));
        context.Set<Activity>().Add(NewEntity(1, 1200));
        await context.SaveChangesAsync();
        var sut = new ActivityRepository(context, Logger());

        var result = await sut.GetByIdAsync(1);

        result.IsSuccess.ShouldBeTrue();
        result.Value.DealId.ShouldBe(1200);
    }

    [Fact]
    public async Task GetByIdAsync_WhenNotFound_ReturnsTheDomainNotFoundError()
    {
        using var context = CreateContext(nameof(GetByIdAsync_WhenNotFound_ReturnsTheDomainNotFoundError));
        var sut = new ActivityRepository(context, Logger());

        var result = await sut.GetByIdAsync(999);

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.NotFound);
        result.Error.Message.ShouldContain("999");
    }

    [Fact]
    public async Task GetByIdAsync_WhenExceptionThrown_ReturnsInternalErrorAndLogs()
    {
        var context = CreateContext(nameof(GetByIdAsync_WhenExceptionThrown_ReturnsInternalErrorAndLogs));
        var logger = Logger();
        var sut = new ActivityRepository(context, logger);
        await context.DisposeAsync();

        var result = await sut.GetByIdAsync(1);

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Internal);
        logger.Received(1).Error(Arg.Is<Exception?>(e => e != null), Arg.Any<string>(), Arg.Any<object[]>());
    }

    // ── ExistsAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task ExistsAsync_WhenTheActivityExists_ReturnsTrue()
    {
        using var context = CreateContext(nameof(ExistsAsync_WhenTheActivityExists_ReturnsTrue));
        context.Set<Activity>().Add(NewEntity(1, 1200));
        await context.SaveChangesAsync();
        var sut = new ActivityRepository(context, Logger());

        (await sut.ExistsAsync(1)).Value.ShouldBeTrue();
    }

    [Fact]
    public async Task ExistsAsync_WithAnUnknownId_ReturnsFalse()
    {
        using var context = CreateContext(nameof(ExistsAsync_WithAnUnknownId_ReturnsFalse));
        var sut = new ActivityRepository(context, Logger());

        (await sut.ExistsAsync(404)).Value.ShouldBeFalse();
    }

    [Fact]
    public async Task ExistsAsync_WhenExceptionThrown_ReturnsInternalErrorAndLogs()
    {
        var context = CreateContext(nameof(ExistsAsync_WhenExceptionThrown_ReturnsInternalErrorAndLogs));
        var logger = Logger();
        var sut = new ActivityRepository(context, logger);
        await context.DisposeAsync();

        var result = await sut.ExistsAsync(1);

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Internal);
        logger.Received(1).Error(Arg.Is<Exception?>(e => e != null), Arg.Any<string>(), Arg.Any<object[]>());
    }

    // ── GetAllAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAllAsync_WhenEmpty_ReturnsEmptyPagedResult()
    {
        using var context = CreateContext(nameof(GetAllAsync_WhenEmpty_ReturnsEmptyPagedResult));
        var sut = new ActivityRepository(context, Logger());

        var result = await sut.GetAllAsync(new PageQuery(0, 10));

        result.IsSuccess.ShouldBeTrue();
        result.Items.ShouldBeEmpty();
        result.TotalCount.ShouldBe(0);
    }

    [Fact]
    public async Task GetAllAsync_WhenMultipleExist_PagesOrderedById()
    {
        using var context = CreateContext(nameof(GetAllAsync_WhenMultipleExist_PagesOrderedById));
        context.Set<Activity>().AddRange(NewEntity(3, 1200), NewEntity(1, 1200), NewEntity(2, 1200));
        await context.SaveChangesAsync();
        var sut = new ActivityRepository(context, Logger());

        var firstPage = await sut.GetAllAsync(new PageQuery(0, 2));
        var secondPage = await sut.GetAllAsync(new PageQuery(1, 2));

        firstPage.TotalCount.ShouldBe(3);
        firstPage.Items.Select(a => a.Id).ShouldBe([1, 2]);
        secondPage.Items.Select(a => a.Id).ShouldBe([3]);
    }

    [Fact]
    public async Task GetAllAsync_WhenExceptionThrown_ReturnsInternalErrorAndLogs()
    {
        var context = CreateContext(nameof(GetAllAsync_WhenExceptionThrown_ReturnsInternalErrorAndLogs));
        var logger = Logger();
        var sut = new ActivityRepository(context, logger);
        await context.DisposeAsync();

        var result = await sut.GetAllAsync(new PageQuery(0, 10));

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Internal);
        logger.Received(1).Error(Arg.Is<Exception?>(e => e != null), Arg.Any<string>(), Arg.Any<object[]>());
    }

    // ── CreateAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_WhenValid_WritesTheRowAndReturnsItWithTheGeneratedId()
    {
        using var context = CreateContext(nameof(CreateAsync_WhenValid_WritesTheRowAndReturnsItWithTheGeneratedId));
        var sut = new ActivityRepository(context, Logger());

        var result = await sut.CreateAsync(NewActivity(1200));

        result.IsSuccess.ShouldBeTrue();
        result.Value.Id.ShouldBeGreaterThan(0, "the identity only exists once the row does");
        (await context.Set<Activity>().CountAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task CreateAsync_WhenExceptionThrown_ReturnsInternalErrorAndLogs()
    {
        var context = CreateContext(nameof(CreateAsync_WhenExceptionThrown_ReturnsInternalErrorAndLogs));
        var logger = Logger();
        var sut = new ActivityRepository(context, logger);
        await context.DisposeAsync();

        var result = await sut.CreateAsync(NewActivity(1200));

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Internal);
        result.Error.Origin.ShouldBe(nameof(ActivityRepository), "the repository seals its own failures");
        logger.Received(1).Error(Arg.Is<Exception?>(e => e != null), Arg.Any<string>(), Arg.Any<object[]>());
    }

    // ── AddAsync ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task AddAsync_WhenValid_StagesTheRowWithoutWritingIt()
    {
        using var context = CreateContext(nameof(AddAsync_WhenValid_StagesTheRowWithoutWritingIt));
        var sut = new ActivityRepository(context, Logger());
        var activity = NewActivity(1200);

        var result = await sut.AddAsync(activity);

        result.IsSuccess.ShouldBeTrue();

        // Queued only: this is the member for a write that joins a larger transaction, so the
        // unit of work is what decides when — and whether — the row exists.
        context.ChangeTracker.Entries<Activity>().Count().ShouldBe(1);
        (await context.Set<Activity>().CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task AddAsync_WhenExceptionThrown_ReturnsInternalErrorAndLogs()
    {
        var context = CreateContext(nameof(AddAsync_WhenExceptionThrown_ReturnsInternalErrorAndLogs));
        var logger = Logger();
        var sut = new ActivityRepository(context, logger);
        await context.DisposeAsync();

        var result = await sut.AddAsync(NewActivity(1200));

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Internal);
        logger.Received(1).Error(Arg.Is<Exception?>(e => e != null), Arg.Any<string>(), Arg.Any<object[]>());
    }

    // ── Update ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Update_WhenValid_MarksTheEntityForUpdate()
    {
        const string dbName = nameof(Update_WhenValid_MarksTheEntityForUpdate);
        var activity = NewActivity(1200);
        using (var seedContext = CreateContext(dbName))
        {
            // Seeded with CreateAsync, not AddAsync: Update needs an aggregate that already
            // carries its identity, or EF reads the default key as a second insert.
            await new ActivityRepository(seedContext, Logger()).CreateAsync(activity);
        }

        // A fresh context, like RepositoryBaseEFTests: Update's own tracked entity would
        // otherwise conflict with the one the insert already tracks for the same key.
        using var context = CreateContext(dbName);
        var sut = new ActivityRepository(context, Logger());

        var result = sut.Update(activity);
        await context.SaveChangesAsync();

        result.IsSuccess.ShouldBeTrue();
        (await context.Set<Activity>().CountAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task Update_WhenExceptionThrown_ReturnsInternalErrorAndLogs()
    {
        var context = CreateContext(nameof(Update_WhenExceptionThrown_ReturnsInternalErrorAndLogs));
        var logger = Logger();
        var sut = new ActivityRepository(context, logger);
        var activity = NewActivity(1200);
        await context.DisposeAsync();

        var result = sut.Update(activity);

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Internal);
        logger.Received(1).Error(Arg.Is<Exception?>(e => e != null), Arg.Any<string>(), Arg.Any<object[]>());
    }

    // ── RemoveAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task RemoveAsync_WhenTheActivityExists_MarksItForDeletion()
    {
        using var context = CreateContext(nameof(RemoveAsync_WhenTheActivityExists_MarksItForDeletion));
        context.Set<Activity>().Add(NewEntity(1, 1200));
        await context.SaveChangesAsync();
        var sut = new ActivityRepository(context, Logger());

        var result = await sut.RemoveAsync(1);
        await context.SaveChangesAsync();

        result.IsSuccess.ShouldBeTrue();
        (await context.Set<Activity>().CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task RemoveAsync_WithAnUnknownId_ReturnsTheDomainNotFoundError()
    {
        using var context = CreateContext(nameof(RemoveAsync_WithAnUnknownId_ReturnsTheDomainNotFoundError));
        var sut = new ActivityRepository(context, Logger());

        var result = await sut.RemoveAsync(404);

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.NotFound);
    }

    [Fact]
    public async Task RemoveAsync_WhenExceptionThrown_ReturnsInternalErrorAndLogs()
    {
        var context = CreateContext(nameof(RemoveAsync_WhenExceptionThrown_ReturnsInternalErrorAndLogs));
        var logger = Logger();
        var sut = new ActivityRepository(context, logger);
        await context.DisposeAsync();

        var result = await sut.RemoveAsync(1);

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Internal);
        logger.Received(1).Error(Arg.Is<Exception?>(e => e != null), Arg.Any<string>(), Arg.Any<object[]>());
    }

    // ── SearchAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task SearchAsync_FiltersAndExcludesActivitiesWithNoMatchingDealOrOpportunity()
    {
        using var context = CreateContext(nameof(SearchAsync_FiltersAndExcludesActivitiesWithNoMatchingDealOrOpportunity));
        context.Set<Opportunity>().Add(new Opportunity { Id = 845, Name = "x", IsArchived = false });
        context.Set<Deal>().Add(new Deal { Id = 1200, OpportunityId = 845, DealStateId = 3, Name = "x" });
        context.Set<Deal>().Add(new Deal { Id = 1300, OpportunityId = 999999, DealStateId = 9, Name = "x" });
        context.Set<Activity>().AddRange(
            NewEntity(1, dealId: 1200, opportunityId: 845),
            NewEntity(2, dealId: 1300), // deal's opportunity does not exist — excluded
            NewEntity(3, dealId: 777777)); // deal does not exist — excluded
        await context.SaveChangesAsync();
        var sut = new ActivityRepository(context, Logger());

        var byDeal = await sut.SearchAsync(new ActivityFilter(1200, null, null), new PageQuery(0, 10));
        var byOpportunity = await sut.SearchAsync(new ActivityFilter(null, 845, null), new PageQuery(0, 10));
        var byDealState = await sut.SearchAsync(new ActivityFilter(null, null, 3), new PageQuery(0, 10));

        byDeal.TotalCount.ShouldBe(1);
        byDeal.Items.ShouldHaveSingleItem().Activity.Id.ShouldBe(1);
        byOpportunity.Items.ShouldHaveSingleItem().Activity.Id.ShouldBe(1);
        byDealState.Items.ShouldHaveSingleItem().Activity.Id.ShouldBe(1);
    }

    [Fact]
    public async Task SearchAsync_CarriesTheDealAndOpportunityNamesOfEachRow()
    {
        using var context = CreateContext(nameof(SearchAsync_CarriesTheDealAndOpportunityNamesOfEachRow));
        context.Set<Opportunity>().Add(new Opportunity { Id = 845, Name = "Oportunidad", IsArchived = false });
        context.Set<Deal>().Add(new Deal { Id = 1200, OpportunityId = 845, DealStateId = 3, Name = "Negocio" });
        context.Set<Activity>().Add(NewEntity(1, dealId: 1200, opportunityId: 845));
        await context.SaveChangesAsync();
        var sut = new ActivityRepository(context, Logger());

        var result = await sut.SearchAsync(new ActivityFilter(1200, null, null), new PageQuery(0, 10));

        var item = result.Items.ShouldHaveSingleItem();
        item.DealName.ShouldBe("Negocio");
        item.OpportunityName.ShouldBe("Oportunidad");

        // The advisor is left-joined: a row without one is still returned, unnamed.
        item.AdvisorName.ShouldBeNull();
        item.AdvisorIdentification.ShouldBeNull();
    }

    [Fact]
    public async Task SearchAsync_WhenExceptionThrown_ReturnsInternalErrorAndLogs()
    {
        var context = CreateContext(nameof(SearchAsync_WhenExceptionThrown_ReturnsInternalErrorAndLogs));
        var logger = Logger();
        var sut = new ActivityRepository(context, logger);
        await context.DisposeAsync();

        var result = await sut.SearchAsync(new ActivityFilter(null, null, null), new PageQuery(0, 10));

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Internal);
        logger.Received(1).Error(Arg.Is<Exception?>(e => e != null), Arg.Any<string>(), Arg.Any<object[]>());
    }
}
