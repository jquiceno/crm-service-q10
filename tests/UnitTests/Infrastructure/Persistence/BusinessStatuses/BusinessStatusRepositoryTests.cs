using BusinessStatus.Domain.Aggregates;
using BusinessStatus.Domain.Enums;
using BusinessStatus.Domain.Errors;
using BusinessStatus.Domain.Queries;
using Infrastructure.Persistence.EntityFramework;
using Infrastructure.Persistence.EntityFramework.BusinessStatuses;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NSubstitute;
using Shared.Application.Ports;
using Shared.Domain.Pagination;
using Shared.Results.Errors;
using Shouldly;
using Xunit;
using Entities = Infrastructure.Persistence.EntityFramework.BusinessStatuses.Entities;

namespace UnitTests.Infrastructure.Persistence.BusinessStatuses;

/// <summary>
/// Exercised against the EF Core in-memory provider with the production
/// <see cref="ApplicationDbContext"/>, so the real configuration and mapper take part. What that
/// provider cannot reproduce — the SQL of <c>OFFSET/FETCH</c>, the <c>decimal(20,5)</c> column and
/// the error 547 — belongs to the integration suite over a real SQL Server.
/// </summary>
public sealed class BusinessStatusRepositoryTests
{
    private readonly ILoggerPort<BusinessStatusRepository> _logger =
        Substitute.For<ILoggerPort<BusinessStatusRepository>>();

    private readonly ICacheStore _cacheStore = Substitute.For<ICacheStore>();

    /// <summary>
    /// No tenant resolved, so the listing skips the L2 cache entirely and every assertion here is
    /// about the query itself. The cache-aside behaviour has its own suite.
    /// </summary>
    private readonly ITenantCodeProvider _tenantCodeProvider = Substitute.For<ITenantCodeProvider>();

    private BusinessStatusRepository NewRepository(ApplicationDbContext context) =>
        new(context, _logger, _cacheStore, _tenantCodeProvider);

    private static readonly PageQuery FirstPage = new(pageIndex: 0, pageSize: 20);

    private static BusinessStatusFilter NoFilter =>
        new(Name: null, IsActive: null, BusinessStatusKind.All);

    private static ApplicationDbContext CreateContext(
        string databaseName, IInterceptor? interceptor = null)
    {
        var builder = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName);

        if (interceptor is not null)
            builder.AddInterceptors(interceptor);

        return new ApplicationDbContext(builder.Options);
    }

    /// <summary>
    /// Seeds through its own context instance, so the repository under test starts with an empty
    /// change tracker.
    /// </summary>
    private static async Task SeedAsync(string databaseName, params Entities.BusinessStatus[] rows)
    {
        using var context = CreateContext(databaseName);
        context.BusinessStatuses.AddRange(rows);
        await context.SaveChangesAsync().ConfigureAwait(false);
    }

    private static Entities.BusinessStatus Row(
        int id,
        string? name = "Negotiation",
        bool? isActive = true,
        decimal? percentage = 50m,
        string? color = "49ff7c") =>
        new()
        {
            Id = id,
            Name = name,
            IsActive = isActive,
            Percentage = percentage,
            Color = color
        };

    private static BusinessStatusAggregate Aggregate(
        int id = 0,
        string name = "Negotiation",
        int? percentage = 50,
        string? color = "49ff7c",
        bool isActive = true) =>
        BusinessStatusAggregate.Reconstruct(id, name, percentage, color, isActive);

    private void ShouldBeAPersistenceFailure(DomainError error)
    {
        error.Type.ShouldBe(ErrorType.Internal);
        error.Origin.ShouldBe(nameof(BusinessStatusRepository));
        _logger.ReceivedWithAnyArgs(1).Error(null, string.Empty);
    }

    // ── GetByIdAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GetByIdAsync_WithAnExistingId_ReturnsTheMappedAggregate()
    {
        var database = nameof(GetByIdAsync_WithAnExistingId_ReturnsTheMappedAggregate);
        await SeedAsync(database, Row(7, name: "Negotiation", percentage: 50m, color: "49ff7c"));
        using var context = CreateContext(database);

        var result = await NewRepository(context).GetByIdAsync(7);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Id.ShouldBe(7);
        result.Value.Name.ShouldBe("Negotiation");
        result.Value.Percentage.ShouldBe(50);
        result.Value.Color!.Value.ShouldBe("49ff7c");
        result.Value.IsActive.ShouldBeTrue();
    }

    [Fact]
    public async Task GetByIdAsync_WithAMissingId_ReturnsNotFoundSealedWithContextAndOrigin()
    {
        var database = nameof(GetByIdAsync_WithAMissingId_ReturnsNotFoundSealedWithContextAndOrigin);
        await SeedAsync(database, Row(7));
        using var context = CreateContext(database);

        var result = await NewRepository(context).GetByIdAsync(999);

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.NotFound);
        result.Error.Context.ShouldBe(BusinessStatusErrors.Context);
        result.Error.Origin.ShouldBe(nameof(BusinessStatusRepository));
    }

    [Fact]
    public async Task GetByIdAsync_WhenTheQueryFails_ReturnsAPersistenceFailureWithoutThrowing()
    {
        var context = CreateContext(nameof(GetByIdAsync_WhenTheQueryFails_ReturnsAPersistenceFailureWithoutThrowing));
        var repository = NewRepository(context);
        await context.DisposeAsync();

        var result = await repository.GetByIdAsync(7);

        result.IsFailure.ShouldBeTrue();
        ShouldBeAPersistenceFailure(result.Error);
    }

    // ── ExistsAsync ───────────────────────────────────────────────────────────

    [Theory]
    [InlineData(7, true)]
    [InlineData(999, false)]
    public async Task ExistsAsync_AnswersWhetherTheRowIsThere(int id, bool expected)
    {
        var database = $"{nameof(ExistsAsync_AnswersWhetherTheRowIsThere)}-{id}";
        await SeedAsync(database, Row(7));
        using var context = CreateContext(database);

        var result = await NewRepository(context).ExistsAsync(id);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(expected);
    }

    [Fact]
    public async Task ExistsAsync_WhenTheQueryFails_ReturnsAPersistenceFailureWithoutThrowing()
    {
        var context = CreateContext(nameof(ExistsAsync_WhenTheQueryFails_ReturnsAPersistenceFailureWithoutThrowing));
        var repository = NewRepository(context);
        await context.DisposeAsync();

        var result = await repository.ExistsAsync(7);

        result.IsFailure.ShouldBeTrue();
        ShouldBeAPersistenceFailure(result.Error);
    }

    // ── GetAsync — order and pagination ───────────────────────────────────────

    [Fact]
    public async Task GetAsync_WithoutFilters_OrdersByPercentageAscending()
    {
        var database = nameof(GetAsync_WithoutFilters_OrdersByPercentageAscending);
        await SeedAsync(
            database,
            Row(1, percentage: 100m),
            Row(2, percentage: 0m),
            Row(3, percentage: 50m));
        using var context = CreateContext(database);

        var result = await NewRepository(context).GetAsync(NoFilter, FirstPage);

        result.IsSuccess.ShouldBeTrue();
        result.TotalCount.ShouldBe(3);
        result.Items.Select(x => x.Percentage).ShouldBe([0, 50, 100]);
    }

    [Fact]
    public async Task GetAsync_WithRowsSharingAPercentage_BreaksTheTieById()
    {
        var database = nameof(GetAsync_WithRowsSharingAPercentage_BreaksTheTieById);
        await SeedAsync(
            database,
            Row(31, percentage: 50m),
            Row(12, percentage: 50m),
            Row(24, percentage: 50m));
        using var context = CreateContext(database);

        var result = await NewRepository(context).GetAsync(NoFilter, FirstPage);

        result.Items.Select(x => x.Id).ShouldBe([12, 24, 31]);
    }

    [Fact]
    public async Task GetAsync_PaginatesInTheQueryAndReportsTheWholeTotal()
    {
        var database = nameof(GetAsync_PaginatesInTheQueryAndReportsTheWholeTotal);
        await SeedAsync(
            database,
            Row(1, percentage: 10m),
            Row(2, percentage: 20m),
            Row(3, percentage: 30m),
            Row(4, percentage: 40m),
            Row(5, percentage: 60m));
        using var context = CreateContext(database);

        var result = await NewRepository(context)
            .GetAsync(NoFilter, new PageQuery(pageIndex: 1, pageSize: 2));

        result.TotalCount.ShouldBe(5, "the total counts every match, not just the page");
        result.Items.Select(x => x.Id).ShouldBe([3, 4]);
    }

    [Fact]
    public async Task GetAsync_WhenTheQueryFails_ReturnsAPersistenceFailureWithoutThrowing()
    {
        var context = CreateContext(nameof(GetAsync_WhenTheQueryFails_ReturnsAPersistenceFailureWithoutThrowing));
        var repository = NewRepository(context);
        await context.DisposeAsync();

        var result = await repository.GetAsync(NoFilter, FirstPage);

        result.IsFailure.ShouldBeTrue();
        ShouldBeAPersistenceFailure(result.Error);
    }

    // ── GetAsync — filters ────────────────────────────────────────────────────

    [Fact]
    public async Task GetAsync_FilteringByName_MatchesPartiallyAndIsNotCaseSensitiveToTheStoredValue()
    {
        var database = nameof(GetAsync_FilteringByName_MatchesPartiallyAndIsNotCaseSensitiveToTheStoredValue);
        await SeedAsync(
            database,
            Row(1, name: "Negotiation", percentage: 10m),
            Row(2, name: "Proposal sent", percentage: 20m),
            Row(3, name: null, percentage: 30m));
        using var context = CreateContext(database);

        var result = await NewRepository(context)
            .GetAsync(new BusinessStatusFilter("gotia", IsActive: null, BusinessStatusKind.All), FirstPage);

        result.Items.Select(x => x.Id).ShouldBe([1]);
        result.TotalCount.ShouldBe(1);
    }

    [Fact]
    public async Task GetAsync_WithABlankName_AppliesNoNameFilter()
    {
        var database = nameof(GetAsync_WithABlankName_AppliesNoNameFilter);
        await SeedAsync(database, Row(1, percentage: 10m), Row(2, percentage: 20m));
        using var context = CreateContext(database);

        var result = await NewRepository(context)
            .GetAsync(new BusinessStatusFilter("   ", IsActive: null, BusinessStatusKind.All), FirstPage);

        result.TotalCount.ShouldBe(2);
    }

    [Theory]
    [InlineData(true, 1)]
    [InlineData(false, 2)]
    public async Task GetAsync_FilteringByActivity_ReturnsOnlyThatSide(bool isActive, int expectedId)
    {
        var database = $"{nameof(GetAsync_FilteringByActivity_ReturnsOnlyThatSide)}-{isActive}";
        await SeedAsync(
            database,
            Row(1, isActive: true, percentage: 10m),
            Row(2, isActive: false, percentage: 20m),
            Row(3, isActive: null, percentage: 30m));
        using var context = CreateContext(database);

        var result = await NewRepository(context)
            .GetAsync(new BusinessStatusFilter(Name: null, isActive, BusinessStatusKind.All), FirstPage);

        result.Items.Select(x => x.Id).ShouldBe([expectedId]);
    }

    [Fact]
    public async Task GetAsync_OmittingTheActivityFilter_ReturnsActiveAndInactiveAlike()
    {
        var database = nameof(GetAsync_OmittingTheActivityFilter_ReturnsActiveAndInactiveAlike);
        await SeedAsync(
            database,
            Row(1, isActive: true, percentage: 10m),
            Row(2, isActive: false, percentage: 20m),
            Row(3, isActive: null, percentage: 30m));
        using var context = CreateContext(database);

        var result = await NewRepository(context).GetAsync(NoFilter, FirstPage);

        result.TotalCount.ShouldBe(3, "an omitted filter is no filter, the semantics of the legacy procedure");
    }

    [Fact]
    public async Task GetAsync_WithKindIntermediate_ExcludesTerminalsAndKeepsRowsWithoutPercentage()
    {
        var database = nameof(GetAsync_WithKindIntermediate_ExcludesTerminalsAndKeepsRowsWithoutPercentage);
        await SeedAsync(
            database,
            Row(1, percentage: 0m),
            Row(2, percentage: 100m),
            Row(3, percentage: 50m),
            Row(4, percentage: null));
        using var context = CreateContext(database);

        var result = await NewRepository(context)
            .GetAsync(new BusinessStatusFilter(Name: null, IsActive: null, BusinessStatusKind.Intermediate), FirstPage);

        result.Items.Select(x => x.Id).ShouldBe([3, 4], ignoreOrder: true);
    }

    [Fact]
    public async Task GetAsync_WithKindTerminal_ReturnsOnlyTheReservedPercentages()
    {
        var database = nameof(GetAsync_WithKindTerminal_ReturnsOnlyTheReservedPercentages);
        await SeedAsync(
            database,
            Row(1, percentage: 0m),
            Row(2, percentage: 100m),
            Row(3, percentage: 50m),
            Row(4, percentage: null));
        using var context = CreateContext(database);

        var result = await NewRepository(context)
            .GetAsync(new BusinessStatusFilter(Name: null, IsActive: null, BusinessStatusKind.Terminal), FirstPage);

        result.Items.Select(x => x.Id).ShouldBe([1, 2]);
        result.Items.ShouldAllBe(x => x.IsTerminal);
    }

    // ── GetAllAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAllAsync_ServesTheWholeCatalogueThroughTheFilteredQuery()
    {
        var database = nameof(GetAllAsync_ServesTheWholeCatalogueThroughTheFilteredQuery);
        await SeedAsync(
            database,
            Row(1, isActive: false, percentage: 0m),
            Row(2, percentage: 100m),
            Row(3, percentage: 50m));
        using var context = CreateContext(database);

        var result = await NewRepository(context).GetAllAsync(FirstPage);

        result.IsSuccess.ShouldBeTrue();
        result.TotalCount.ShouldBe(3);
        result.Items.Select(x => x.Id).ShouldBe([1, 3, 2]);
    }

    // ── GetActiveTerminalsAsync ───────────────────────────────────────────────

    [Fact]
    public async Task GetActiveTerminalsAsync_Won_ReturnsEveryActiveCandidateAndNeverAnInactiveOne()
    {
        // The broken tenant of the discovery: row 5 is an inactive 100 % and row 21 an active one.
        var database = nameof(GetActiveTerminalsAsync_Won_ReturnsEveryActiveCandidateAndNeverAnInactiveOne);
        await SeedAsync(
            database,
            Row(5, isActive: false, percentage: 100m),
            Row(21, isActive: true, percentage: 100m),
            Row(30, isActive: true, percentage: 100m),
            Row(40, isActive: true, percentage: 0m));
        using var context = CreateContext(database);

        var result = await NewRepository(context)
            .GetActiveTerminalsAsync(TerminalKind.Won);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Select(x => x.Id).ShouldBe([21, 30], "the repository reports the ambiguity, it does not resolve it");
    }

    [Fact]
    public async Task GetActiveTerminalsAsync_Lost_ReturnsTheActiveZero()
    {
        var database = nameof(GetActiveTerminalsAsync_Lost_ReturnsTheActiveZero);
        await SeedAsync(
            database,
            Row(1, isActive: true, percentage: 0m),
            Row(2, isActive: true, percentage: 100m),
            Row(3, isActive: null, percentage: 0m));
        using var context = CreateContext(database);

        var result = await NewRepository(context)
            .GetActiveTerminalsAsync(TerminalKind.Lost);

        result.Value.Select(x => x.Id).ShouldBe([1]);
        result.Value.ShouldAllBe(x => x.IsLost);
    }

    [Fact]
    public async Task GetActiveTerminalsAsync_WithoutAnyCandidate_ReturnsAnEmptyList()
    {
        var database = nameof(GetActiveTerminalsAsync_WithoutAnyCandidate_ReturnsAnEmptyList);
        await SeedAsync(database, Row(1, percentage: 50m));
        using var context = CreateContext(database);

        var result = await NewRepository(context)
            .GetActiveTerminalsAsync(TerminalKind.Won);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeEmpty("deciding that nothing was found belongs to the provider");
    }

    [Fact]
    public async Task GetActiveTerminalsAsync_WhenTheQueryFails_ReturnsAPersistenceFailureWithoutThrowing()
    {
        var context = CreateContext(nameof(GetActiveTerminalsAsync_WhenTheQueryFails_ReturnsAPersistenceFailureWithoutThrowing));
        var repository = NewRepository(context);
        await context.DisposeAsync();

        var result = await repository.GetActiveTerminalsAsync(TerminalKind.Won);

        result.IsFailure.ShouldBeTrue();
        ShouldBeAPersistenceFailure(result.Error);
    }

    // ── CreateAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_CommitsAndReturnsTheAggregateWithTheGeneratedIdentity()
    {
        var database = nameof(CreateAsync_CommitsAndReturnsTheAggregateWithTheGeneratedIdentity);
        using var context = CreateContext(database);

        var result = await NewRepository(context)
            .CreateAsync(Aggregate(name: "Negotiation", percentage: 50, color: "49ff7c"));

        result.IsSuccess.ShouldBeTrue();
        result.Value.Id.ShouldBeGreaterThan(0, "the caller needs the identity the database assigned");

        using var verification = CreateContext(database);
        var persisted = await verification.BusinessStatuses.SingleAsync();
        persisted.Id.ShouldBe(result.Value.Id);
        persisted.Name.ShouldBe("Negotiation");
        persisted.Percentage.ShouldBe(50m);
        persisted.Color.ShouldBe("49ff7c");
        persisted.IsActive.ShouldBe(true);
    }

    [Fact]
    public async Task CreateAsync_CompletesTheSameAggregateInsteadOfRebuildingIt()
    {
        using var context = CreateContext(nameof(CreateAsync_CompletesTheSameAggregateInsteadOfRebuildingIt));
        var aggregate = BusinessStatusAggregate
            .Create(new CreateBusinessStatusArgs("Negotiation", 50m, "49ff7c", IsActive: true))
            .Value;

        var result = await NewRepository(context).CreateAsync(aggregate);

        result.Value.ShouldBeSameAs(aggregate);
        result.Value.Id.ShouldBeGreaterThan(0);
        result.Value.CreatedAt.ShouldNotBeNull("whatever Create set has to survive the insert");
        result.Value.UpdatedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task CreateAsync_WithoutColor_PersistsNullAndNeverTheLegacyDefault()
    {
        var database = nameof(CreateAsync_WithoutColor_PersistsNullAndNeverTheLegacyDefault);
        using var context = CreateContext(database);

        var result = await NewRepository(context)
            .CreateAsync(Aggregate(color: null));

        result.IsSuccess.ShouldBeTrue();
        result.Value.Color.ShouldBeNull();

        using var verification = CreateContext(database);
        (await verification.BusinessStatuses.SingleAsync()).Color.ShouldBeNull();
    }

    [Fact]
    public async Task CreateAsync_WhenTheCommitFailsOnTheDatabase_ReturnsTheClassifiedError()
    {
        using var context = CreateContext(
            nameof(CreateAsync_WhenTheCommitFailsOnTheDatabase_ReturnsTheClassifiedError),
            new ThrowingSaveInterceptor(new DbUpdateException("insert failed", new InvalidOperationException())));

        var result = await NewRepository(context).CreateAsync(Aggregate());

        result.IsFailure.ShouldBeTrue();
        ShouldBeAPersistenceFailure(result.Error);
    }

    [Fact]
    public async Task CreateAsync_WhenTheCommitFailsUnexpectedly_ReturnsAPersistenceFailure()
    {
        using var context = CreateContext(
            nameof(CreateAsync_WhenTheCommitFailsUnexpectedly_ReturnsAPersistenceFailure),
            new ThrowingSaveInterceptor(new TimeoutException("the socket gave up")));

        var result = await NewRepository(context).CreateAsync(Aggregate());

        result.IsFailure.ShouldBeTrue();
        ShouldBeAPersistenceFailure(result.Error);
    }

    // ── AddAsync ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task AddAsync_QueuesTheInsertWithoutCommittingIt()
    {
        var database = nameof(AddAsync_QueuesTheInsertWithoutCommittingIt);
        using var context = CreateContext(database);

        var result = await NewRepository(context).AddAsync(Aggregate());

        result.IsSuccess.ShouldBeTrue();
        context.ChangeTracker.Entries<Entities.BusinessStatus>()
            .ShouldHaveSingleItem().State.ShouldBe(EntityState.Added);

        using var verification = CreateContext(database);
        (await verification.BusinessStatuses.CountAsync())
            .ShouldBe(0, "the commit belongs to the Unit of Work");
    }

    [Fact]
    public async Task AddAsync_WhenTheContextIsUnusable_ReturnsAPersistenceFailureWithoutThrowing()
    {
        var context = CreateContext(nameof(AddAsync_WhenTheContextIsUnusable_ReturnsAPersistenceFailureWithoutThrowing));
        var repository = NewRepository(context);
        await context.DisposeAsync();

        var result = await repository.AddAsync(Aggregate());

        result.IsFailure.ShouldBeTrue();
        ShouldBeAPersistenceFailure(result.Error);
    }

    // ── Update ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Update_ReplacesEveryColumnOfTheRow()
    {
        var database = nameof(Update_ReplacesEveryColumnOfTheRow);
        await SeedAsync(database, Row(7, name: "Negotiation", isActive: true, percentage: 50m, color: "49ff7c"));
        using var context = CreateContext(database);

        var result = NewRepository(context)
            .Update(Aggregate(id: 7, name: "Renegotiation", percentage: 60, color: null, isActive: false));

        result.IsSuccess.ShouldBeTrue();
        await context.SaveChangesAsync();

        using var verification = CreateContext(database);
        var persisted = await verification.BusinessStatuses.SingleAsync(x => x.Id == 7);
        persisted.Name.ShouldBe("Renegotiation");
        persisted.Percentage.ShouldBe(60m);
        persisted.IsActive.ShouldBe(false);
        persisted.Color.ShouldBeNull("an omitted field is erased, not silently kept");
    }

    [Fact]
    public void Update_WhenTheContextIsUnusable_ReturnsAPersistenceFailureWithoutThrowing()
    {
        var context = CreateContext(nameof(Update_WhenTheContextIsUnusable_ReturnsAPersistenceFailureWithoutThrowing));
        var repository = NewRepository(context);
        context.Dispose();

        var result = repository.Update(Aggregate(id: 7));

        result.IsFailure.ShouldBeTrue();
        ShouldBeAPersistenceFailure(result.Error);
    }

    // ── RemoveAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task RemoveAsync_WithAnExistingId_MarksTheRowForDeletion()
    {
        var database = nameof(RemoveAsync_WithAnExistingId_MarksTheRowForDeletion);
        await SeedAsync(database, Row(7));
        using var context = CreateContext(database);

        var result = await NewRepository(context).RemoveAsync(7);

        result.IsSuccess.ShouldBeTrue();
        context.ChangeTracker.Entries<Entities.BusinessStatus>()
            .ShouldHaveSingleItem().State.ShouldBe(EntityState.Deleted);

        await context.SaveChangesAsync();

        using var verification = CreateContext(database);
        (await verification.BusinessStatuses.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task RemoveAsync_WithAMissingId_ReturnsNotFoundWithoutTouchingTheStore()
    {
        var database = nameof(RemoveAsync_WithAMissingId_ReturnsNotFoundWithoutTouchingTheStore);
        await SeedAsync(database, Row(7));
        using var context = CreateContext(database);

        var result = await NewRepository(context).RemoveAsync(999);

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.NotFound);
        result.Error.Context.ShouldBe(BusinessStatusErrors.Context);
        result.Error.Origin.ShouldBe(nameof(BusinessStatusRepository));
        context.ChangeTracker.Entries<Entities.BusinessStatus>().ShouldBeEmpty();
    }

    [Fact]
    public async Task RemoveAsync_WhenTheQueryFails_ReturnsAPersistenceFailureWithoutThrowing()
    {
        var context = CreateContext(nameof(RemoveAsync_WhenTheQueryFails_ReturnsAPersistenceFailureWithoutThrowing));
        var repository = NewRepository(context);
        await context.DisposeAsync();

        var result = await repository.RemoveAsync(7);

        result.IsFailure.ShouldBeTrue();
        ShouldBeAPersistenceFailure(result.Error);
    }

    /// <summary>
    /// The only way to reach the write error paths without a real database: the in-memory provider
    /// never rejects a save.
    /// </summary>
    private sealed class ThrowingSaveInterceptor(Exception exception) : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default) =>
            throw exception;
    }
}
