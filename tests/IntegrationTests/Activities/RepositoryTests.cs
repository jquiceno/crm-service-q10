using Activities.Domain.Aggregates;
using Activities.Domain.Enums;
using Activities.Domain.Filters;
using Activities.Domain.ValueObjects;
using Infrastructure.Persistence.EntityFramework.Activities;
using Infrastructure.Persistence.EntityFramework.Activities.Entities;
using IntegrationTests.Infrastructure;
using NSubstitute;
using Shared.Application.Ports;
using Shared.Domain.Pagination;
using Shared.Results.Errors;
using Shouldly;
using Xunit;

namespace IntegrationTests.Activities;

/// <summary>F2.4: <c>AddAsync</c> + generated id, and <c>SearchAsync</c> filter/page/order parity with the legacy SP.</summary>
[Collection(IntegrationTestCollection.Name)]
public sealed class RepositoryTests(SqlServerContainerFixture fixture) : IntegrationTestBase(fixture)
{
    private static PersonCode Advisor => PersonCode.Create("advisor-01").Value;
    private static PersonCode Creator => PersonCode.Create("creator-01").Value;

    private ActivityRepositoryAdapter Sut =>
        new(Db, Substitute.For<ILoggerPort<ActivityRepositoryAdapter>>());

    // --- AddAsync --------------------------------------------------------------------------

    [Fact]
    public async Task AddAsync_PersistsTheRowAndAssignsTheGeneratedId()
    {
        var dueAt = DateTime.UtcNow.AddDays(1);
        var activity = Activity.Schedule(
            1200, 845, ActivityType.Call, Description.Create("call back").Value, dueAt,
            Advisor, Creator).Value;

        var sut = Sut;

        (await sut.AddAsync(activity)).IsSuccess.ShouldBeTrue();

        activity.Id.ShouldBeGreaterThan(0, "AddAsync saves immediately and assigns the generated id");

        var fetched = await sut.GetByIdAsync(activity.Id);
        fetched.IsSuccess.ShouldBeTrue();
        fetched.Value.DealId.ShouldBe(1200);
        fetched.Value.Description!.Value.ShouldBe("call back");
    }

    [Fact]
    public async Task GetByIdAsync_WithAnUnknownId_ReturnsTheDomainNotFoundError()
    {
        var result = await Sut.GetByIdAsync(999999);

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.NotFound);
        result.Error.Message.ShouldContain("999999");
    }

    // --- SearchAsync -------------------------------------------------------------------------

    [Fact]
    public async Task SearchAsync_FiltersByDealId()
    {
        await SeedDealAsync(dealId: 1200, opportunityId: 845, dealStateId: 3);
        await SeedDealAsync(dealId: 1300, opportunityId: 846, dealStateId: 3);
        await SeedActivityAsync(dealId: 1200);
        await SeedActivityAsync(dealId: 1300);

        var result = await Sut.SearchAsync(new ActivityFilter(1200, null, null), new PageQuery(0, 10));

        result.IsSuccess.ShouldBeTrue();
        result.TotalCount.ShouldBe(1);
        result.Items.ShouldHaveSingleItem().DealId.ShouldBe(1200);
    }

    [Fact]
    public async Task SearchAsync_FiltersByOpportunityId()
    {
        await SeedDealAsync(dealId: 1200, opportunityId: 845, dealStateId: 3);
        await SeedDealAsync(dealId: 1300, opportunityId: 846, dealStateId: 3);
        await SeedActivityAsync(dealId: 1200, opportunityId: 845);
        await SeedActivityAsync(dealId: 1300, opportunityId: 846);

        var result = await Sut.SearchAsync(
            new ActivityFilter(null, OpportunityId: 846, null), new PageQuery(0, 10));

        result.TotalCount.ShouldBe(1);
        result.Items.ShouldHaveSingleItem().DealId.ShouldBe(1300);
    }

    [Fact]
    public async Task SearchAsync_FiltersByDealStateId()
    {
        await SeedDealAsync(dealId: 1200, opportunityId: 845, dealStateId: 3);
        await SeedDealAsync(dealId: 1300, opportunityId: 846, dealStateId: 9);
        await SeedActivityAsync(dealId: 1200);
        await SeedActivityAsync(dealId: 1300);

        var result = await Sut.SearchAsync(new ActivityFilter(null, null, DealStateId: 9), new PageQuery(0, 10));

        result.TotalCount.ShouldBe(1);
        result.Items.ShouldHaveSingleItem().DealId.ShouldBe(1300);
    }

    [Fact]
    public async Task SearchAsync_ExcludesActivitiesWhoseDealDoesNotExist()
    {
        await SeedActivityAsync(dealId: 999999);

        var result = await Sut.SearchAsync(new ActivityFilter(null, null, null), new PageQuery(0, 10));

        result.TotalCount.ShouldBe(0);
    }

    [Fact]
    public async Task SearchAsync_ExcludesActivitiesWhoseDealsOpportunityDoesNotExist()
    {
        Db.Set<Deal>().Add(new Deal { Id = 1400, OpportunityId = 777777, DealStateId = 3, Name = "x" });
        await Db.SaveChangesAsync();
        await SeedActivityAsync(dealId: 1400);

        var result = await Sut.SearchAsync(new ActivityFilter(null, null, null), new PageQuery(0, 10));

        result.TotalCount.ShouldBe(0);
    }

    [Fact]
    public async Task SearchAsync_OrdersByIdAscending_AndPaginates()
    {
        await SeedDealAsync(dealId: 1200, opportunityId: 845, dealStateId: 3);
        var ids = new List<int>
        {
            await SeedActivityAsync(dealId: 1200),
            await SeedActivityAsync(dealId: 1200),
            await SeedActivityAsync(dealId: 1200),
        };

        var firstPage = await Sut.SearchAsync(
            new ActivityFilter(1200, null, null), new PageQuery(pageIndex: 0, pageSize: 2));
        var secondPage = await Sut.SearchAsync(
            new ActivityFilter(1200, null, null), new PageQuery(pageIndex: 1, pageSize: 2));

        firstPage.TotalCount.ShouldBe(3);
        firstPage.Items.Select(activity => activity.Id).ShouldBe(ids.Take(2));
        secondPage.Items.Select(activity => activity.Id).ShouldBe(ids.Skip(2).Take(2));
    }

    // --- Seeding -------------------------------------------------------------------------

    private async Task SeedDealAsync(int dealId, int opportunityId, int dealStateId)
    {
        Db.Set<Opportunity>().Add(new Opportunity
        {
            Id = opportunityId,
            Name = "Oportunidad de prueba",
            IsArchived = false,
        });

        Db.Set<Deal>().Add(new Deal
        {
            Id = dealId,
            OpportunityId = opportunityId,
            DealStateId = dealStateId,
            Name = "Negocio de prueba",
        });

        await Db.SaveChangesAsync().ConfigureAwait(false);
    }

    private async Task<int> SeedActivityAsync(int dealId, int? opportunityId = null)
    {
        var entity = new ActivityEntity
        {
            DealId = dealId,
            OpportunityId = opportunityId,
            Type = "1",
            CreatedAt = DateTime.UtcNow,
            CreatedById = "creator-01",
        };

        Db.Activities.Add(entity);
        await Db.SaveChangesAsync().ConfigureAwait(false);
        return entity.Id;
    }
}
