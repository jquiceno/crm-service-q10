using Activities.Domain.Aggregates;
using Activities.Domain.Enums;
using Activities.Domain.Queries;
using Activities.Domain.ValueObjects;
using Infrastructure.Adapters.Persistence;
using Infrastructure.Persistence.EntityFramework;
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

/// <summary>
/// F2.4/F2.6: <c>CreateAsync</c> + generated id, and <c>SearchAsync</c> filter/page/order parity
/// with the legacy SP, proven against both measured schema variants (Discovery §4.1-bis).
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public sealed class RepositoryTests : IAsyncLifetime
{
    private static PersonCode Advisor => PersonCode.Create("advisor-01").Value;
    private static PersonCode Creator => PersonCode.Create("creator-01").Value;

    public static TheoryData<string> Variants => ActivitySchemaVariants.Variants;

    private readonly SqlServerContainerFixture _fixture;

    public RepositoryTests(SqlServerContainerFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        await ActivitySchemaVariants.EnsureCreatedAsync(_fixture, ActivitySchemaVariants.Universal15)
            .ConfigureAwait(false);
        await ActivitySchemaVariants.EnsureCreatedAsync(_fixture, ActivitySchemaVariants.Extended16)
            .ConfigureAwait(false);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private static ActivityRepository Sut(ApplicationDbContext context) =>
        new(context, Substitute.For<ILoggerPort<ActivityRepository>>());

    // --- CreateAsync -------------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(Variants))]
    public async Task CreateAsync_PersistsTheRowAndReturnsTheGeneratedId(string variant)
    {
        using var context = ActivitySchemaVariants.CreateContext(_fixture, variant);
        var sut = Sut(context);
        var dueAt = DateTime.UtcNow.AddDays(1);
        var activity = ActivityAggregate.Schedule(
            1200, 845, ActivityType.Call, Description.Create("call back").Value, dueAt,
            Advisor, Creator).Value;

        var created = await sut.CreateAsync(activity).ConfigureAwait(true);

        // The consecutive is a SQL IDENTITY: it only exists once the insert is confirmed, which is
        // why this member commits its own write instead of queueing it.
        created.IsSuccess.ShouldBeTrue();
        created.Value.Id.ShouldBeGreaterThan(0);

        var fetched = await sut.GetByIdAsync(created.Value.Id).ConfigureAwait(true);
        fetched.IsSuccess.ShouldBeTrue();
        fetched.Value.DealId.ShouldBe(1200);
        fetched.Value.Description!.Value.ShouldBe("call back");
    }

    [Theory]
    [MemberData(nameof(Variants))]
    public async Task AddAsync_WithoutACommit_LeavesNoRowBehind(string variant)
    {
        using var context = ActivitySchemaVariants.CreateContext(_fixture, variant);
        var activity = ActivityAggregate.Schedule(
            1200, 845, ActivityType.Call, Description.Create("call back").Value,
            DateTime.UtcNow.AddDays(1), Advisor, Creator).Value;

        // AddAsync is the member for a write that joins a larger transaction: it only queues.
        await Sut(context).AddAsync(activity).ConfigureAwait(true);

        using var reader = ActivitySchemaVariants.CreateContext(_fixture, variant);
        var all = await Sut(reader).GetAllAsync(new PageQuery(0, 10)).ConfigureAwait(true);
        all.TotalCount.ShouldBe(0);
    }

    [Theory]
    [MemberData(nameof(Variants))]
    public async Task GetByIdAsync_WithAnUnknownId_ReturnsTheDomainNotFoundError(string variant)
    {
        using var context = ActivitySchemaVariants.CreateContext(_fixture, variant);

        var result = await Sut(context).GetByIdAsync(999999).ConfigureAwait(true);

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.NotFound);
        result.Error.Message.ShouldContain("999999");
    }

    // --- SearchAsync -------------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(Variants))]
    public async Task SearchAsync_FiltersByDealId(string variant)
    {
        using var context = ActivitySchemaVariants.CreateContext(_fixture, variant);
        await SeedDealAsync(context, dealId: 1200, opportunityId: 845, dealStateId: 3).ConfigureAwait(true);
        await SeedDealAsync(context, dealId: 1300, opportunityId: 846, dealStateId: 3).ConfigureAwait(true);
        await SeedActivityAsync(context, dealId: 1200).ConfigureAwait(true);
        await SeedActivityAsync(context, dealId: 1300).ConfigureAwait(true);

        var result = await Sut(context)
            .SearchAsync(new ActivityFilter(1200, null, null), new PageQuery(0, 10)).ConfigureAwait(true);

        result.IsSuccess.ShouldBeTrue();
        result.TotalCount.ShouldBe(1);
        result.Items.ShouldHaveSingleItem().Activity.DealId.ShouldBe(1200);
    }

    [Theory]
    [MemberData(nameof(Variants))]
    public async Task SearchAsync_FiltersByOpportunityId(string variant)
    {
        using var context = ActivitySchemaVariants.CreateContext(_fixture, variant);
        await SeedDealAsync(context, dealId: 1200, opportunityId: 845, dealStateId: 3).ConfigureAwait(true);
        await SeedDealAsync(context, dealId: 1300, opportunityId: 846, dealStateId: 3).ConfigureAwait(true);
        await SeedActivityAsync(context, dealId: 1200, opportunityId: 845).ConfigureAwait(true);
        await SeedActivityAsync(context, dealId: 1300, opportunityId: 846).ConfigureAwait(true);

        var result = await Sut(context)
            .SearchAsync(new ActivityFilter(null, OpportunityId: 846, null), new PageQuery(0, 10))
            .ConfigureAwait(true);

        result.TotalCount.ShouldBe(1);
        result.Items.ShouldHaveSingleItem().Activity.DealId.ShouldBe(1300);
    }

    [Theory]
    [MemberData(nameof(Variants))]
    public async Task SearchAsync_FiltersByDealStateId(string variant)
    {
        using var context = ActivitySchemaVariants.CreateContext(_fixture, variant);
        await SeedDealAsync(context, dealId: 1200, opportunityId: 845, dealStateId: 3).ConfigureAwait(true);
        await SeedDealAsync(context, dealId: 1300, opportunityId: 846, dealStateId: 9).ConfigureAwait(true);
        await SeedActivityAsync(context, dealId: 1200).ConfigureAwait(true);
        await SeedActivityAsync(context, dealId: 1300).ConfigureAwait(true);

        var result = await Sut(context)
            .SearchAsync(new ActivityFilter(null, null, DealStateId: 9), new PageQuery(0, 10))
            .ConfigureAwait(true);

        result.TotalCount.ShouldBe(1);
        result.Items.ShouldHaveSingleItem().Activity.DealId.ShouldBe(1300);
    }

    [Theory]
    [MemberData(nameof(Variants))]
    public async Task SearchAsync_ExcludesActivitiesWhoseDealDoesNotExist(string variant)
    {
        using var context = ActivitySchemaVariants.CreateContext(_fixture, variant);
        await SeedActivityAsync(context, dealId: 999999).ConfigureAwait(true);

        var result = await Sut(context)
            .SearchAsync(new ActivityFilter(null, null, null), new PageQuery(0, 10)).ConfigureAwait(true);

        result.TotalCount.ShouldBe(0);
    }

    [Theory]
    [MemberData(nameof(Variants))]
    public async Task SearchAsync_ExcludesActivitiesWhoseDealsOpportunityDoesNotExist(string variant)
    {
        using var context = ActivitySchemaVariants.CreateContext(_fixture, variant);
        context.Set<Deal>().Add(new Deal { Id = 1400, OpportunityId = 777777, DealStateId = 3, Name = "x" });
        await context.SaveChangesAsync().ConfigureAwait(true);
        await SeedActivityAsync(context, dealId: 1400).ConfigureAwait(true);

        var result = await Sut(context)
            .SearchAsync(new ActivityFilter(null, null, null), new PageQuery(0, 10)).ConfigureAwait(true);

        result.TotalCount.ShouldBe(0);
    }

    [Theory]
    [MemberData(nameof(Variants))]
    public async Task SearchAsync_OrdersByIdAscending_AndPaginates(string variant)
    {
        using var context = ActivitySchemaVariants.CreateContext(_fixture, variant);
        await SeedDealAsync(context, dealId: 1200, opportunityId: 845, dealStateId: 3).ConfigureAwait(true);
        var ids = new List<int>
        {
            await SeedActivityAsync(context, dealId: 1200).ConfigureAwait(true),
            await SeedActivityAsync(context, dealId: 1200).ConfigureAwait(true),
            await SeedActivityAsync(context, dealId: 1200).ConfigureAwait(true),
        };

        var sut = Sut(context);
        var firstPage = await sut
            .SearchAsync(new ActivityFilter(1200, null, null), new PageQuery(pageIndex: 0, pageSize: 2))
            .ConfigureAwait(true);
        var secondPage = await sut
            .SearchAsync(new ActivityFilter(1200, null, null), new PageQuery(pageIndex: 1, pageSize: 2))
            .ConfigureAwait(true);

        firstPage.TotalCount.ShouldBe(3);
        firstPage.Items.Select(item => item.Activity.Id).ShouldBe(ids.Take(2));
        secondPage.Items.Select(item => item.Activity.Id).ShouldBe(ids.Skip(2).Take(2));
    }

    [Theory]
    [MemberData(nameof(Variants))]
    public async Task SearchAsync_ResolvesTheDealOpportunityAdvisorAndCreatorNames(string variant)
    {
        using var context = ActivitySchemaVariants.CreateContext(_fixture, variant);
        await SeedDealAsync(context, dealId: 1200, opportunityId: 845, dealStateId: 3).ConfigureAwait(true);
        await SeedPersonAsync(context, code: "advisor-01", identification: "1017123456", fullName: "Ana Pérez")
            .ConfigureAwait(true);
        await SeedPersonAsync(context, code: "creator-01", identification: "1019876543", fullName: "Carlos Ruiz")
            .ConfigureAwait(true);
        await SeedActivityAsync(context, dealId: 1200, opportunityId: 845, advisorId: "advisor-01")
            .ConfigureAwait(true);

        var result = await Sut(context)
            .SearchAsync(new ActivityFilter(1200, null, null), new PageQuery(0, 10)).ConfigureAwait(true);

        var item = result.Items.ShouldHaveSingleItem();
        item.DealName.ShouldBe("Negocio de prueba");
        item.OpportunityName.ShouldBe("Oportunidad de prueba");
        item.AdvisorName.ShouldBe("Ana Pérez");
        item.AdvisorIdentification.ShouldBe("1017123456");
        item.CreatedByName.ShouldBe("Carlos Ruiz");
    }

    [Theory]
    [MemberData(nameof(Variants))]
    public async Task SearchAsync_WithoutAnAdvisorOrACreatorPersonRow_StillReturnsTheRow(string variant)
    {
        using var context = ActivitySchemaVariants.CreateContext(_fixture, variant);
        await SeedDealAsync(context, dealId: 1200, opportunityId: 845, dealStateId: 3).ConfigureAwait(true);
        await SeedActivityAsync(context, dealId: 1200).ConfigureAwait(true);

        var result = await Sut(context)
            .SearchAsync(new ActivityFilter(1200, null, null), new PageQuery(0, 10)).ConfigureAwait(true);

        var item = result.Items.ShouldHaveSingleItem();
        item.Activity.AdvisorId.ShouldBeNull();
        item.AdvisorName.ShouldBeNull();
        item.AdvisorIdentification.ShouldBeNull();
        // CreatedById itself is never null (SeedActivityAsync always sets it), but no Person
        // row exists for "creator-01" here — the LEFT JOIN just leaves the name unresolved.
        item.CreatedByName.ShouldBeNull();
        item.DealName.ShouldBe("Negocio de prueba");
    }

    // --- Seeding -------------------------------------------------------------------------

    private static async Task SeedDealAsync(
        ApplicationDbContext context, int dealId, int opportunityId, int dealStateId)
    {
        context.Set<Opportunity>().Add(new Opportunity
        {
            Id = opportunityId,
            Name = "Oportunidad de prueba",
            IsArchived = false,
        });

        context.Set<Deal>().Add(new Deal
        {
            Id = dealId,
            OpportunityId = opportunityId,
            DealStateId = dealStateId,
            Name = "Negocio de prueba",
        });

        await context.SaveChangesAsync().ConfigureAwait(false);
    }

    private static async Task SeedPersonAsync(
        ApplicationDbContext context, string code, string identification, string fullName)
    {
        context.Set<Person>().Add(new Person
        {
            Code = code,
            Identification = identification,
            FullName = fullName,
        });

        await context.SaveChangesAsync().ConfigureAwait(false);
    }

    private static async Task<int> SeedActivityAsync(
        ApplicationDbContext context, int dealId, int? opportunityId = null, string? advisorId = null)
    {
        var entity = new Activity
        {
            DealId = dealId,
            OpportunityId = opportunityId,
            Type = "1",
            CreatedAt = DateTime.UtcNow,
            CreatedById = "creator-01",
            AdvisorId = advisorId,
        };

        context.Activities.Add(entity);
        await context.SaveChangesAsync().ConfigureAwait(false);
        return entity.Id;
    }
}
