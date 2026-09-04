using Infrastructure.Persistence.EntityFramework;
using Infrastructure.Persistence.EntityFramework.LossReasons;
using LossReason.Domain.Aggregates;
using LossReason.Domain.Queries;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Shared.Application.Ports;
using Shared.Domain.Pagination;
using Shared.Results;
using Shared.Results.Errors;
using Shouldly;
using Xunit;
using LossReasonDocument = Infrastructure.Persistence.EntityFramework.LossReasons.Entities.LossReason;

namespace UnitTests.Infrastructure.Persistence.LossReasons;

/// <summary>
/// Runs the repository against a real <see cref="ApplicationDbContext"/> on the in-memory
/// provider. What that provider cannot honour — constraints, store-generated keys, column types
/// and the server collation — is covered by the integration tests instead.
/// </summary>
public sealed class LossReasonRepositoryTests
{
    private const string Origin = nameof(LossReasonRepository);

    private readonly ILoggerPort<LossReasonRepository> _logger =
        Substitute.For<ILoggerPort<LossReasonRepository>>();

    private static ApplicationDbContext CreateContext(string databaseName) =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options);

    private static async Task<ApplicationDbContext> CreateSeededContextAsync(
        string databaseName,
        params LossReasonDocument[] documents)
    {
        var context = CreateContext(databaseName);
        context.LossReasons.AddRange(documents);
        await context.SaveChangesAsync().ConfigureAwait(false);
        // A repository always starts from a clean change tracker: it gets a per-request context.
        context.ChangeTracker.Clear();
        return context;
    }

    private LossReasonRepository CreateRepository(ApplicationDbContext context) => new(context, _logger);

    private static LossReasonDocument LossReasonRow(int id, string name, bool isActive) =>
        new() { Id = id, Name = name, IsActive = isActive };

    private static PageQuery FirstPage => new(pageIndex: 0, pageSize: 20);

    [Fact]
    public async Task GetByIdAsync_WithExistingRow_ReturnsMappedAggregate()
    {
        using var context = await CreateSeededContextAsync(
            nameof(GetByIdAsync_WithExistingRow_ReturnsMappedAggregate),
            LossReasonRow(7, "Precio", isActive: true));

        var result = await CreateRepository(context).GetByIdAsync(7);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Id.ShouldBe(7);
        result.Value.Name.ShouldBe("Precio");
        result.Value.IsActive.ShouldBeTrue();
    }

    [Fact]
    public async Task GetByIdAsync_WithMissingRow_ReturnsNotFoundStampedWithItsOrigin()
    {
        using var context = CreateContext(nameof(GetByIdAsync_WithMissingRow_ReturnsNotFoundStampedWithItsOrigin));

        var result = await CreateRepository(context).GetByIdAsync(404);

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.NotFound);
        result.Error.Origin.ShouldBe(Origin);
    }

    [Fact]
    public async Task GetByIdAsync_WhenTheQueryFails_ReturnsPersistenceFailure()
    {
        var result = await ExecuteAgainstDisposedContextAsync(
            nameof(GetByIdAsync_WhenTheQueryFails_ReturnsPersistenceFailure),
            repository => repository.GetByIdAsync(7));

        ShouldBePersistenceFailure(result);
    }

    [Fact]
    public async Task ExistsAsync_WithExistingRow_ReturnsTrue()
    {
        using var context = await CreateSeededContextAsync(
            nameof(ExistsAsync_WithExistingRow_ReturnsTrue),
            LossReasonRow(7, "Precio", isActive: true));

        var result = await CreateRepository(context).ExistsAsync(7);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeTrue();
    }

    [Fact]
    public async Task ExistsAsync_WithMissingRow_ReturnsFalseInsteadOfFailing()
    {
        using var context = CreateContext(nameof(ExistsAsync_WithMissingRow_ReturnsFalseInsteadOfFailing));

        var result = await CreateRepository(context).ExistsAsync(404);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeFalse();
    }

    [Fact]
    public async Task ExistsAsync_WhenTheQueryFails_ReturnsPersistenceFailure()
    {
        var result = await ExecuteAgainstDisposedContextAsync(
            nameof(ExistsAsync_WhenTheQueryFails_ReturnsPersistenceFailure),
            repository => repository.ExistsAsync(7));

        ShouldBePersistenceFailure(result);
    }

    [Fact]
    public async Task GetAsync_WithoutFilter_OrdersByNameAndBreaksTiesWithTheKey()
    {
        using var context = await CreateSeededContextAsync(
            nameof(GetAsync_WithoutFilter_OrdersByNameAndBreaksTiesWithTheKey),
            LossReasonRow(3, "Precio", isActive: true),
            LossReasonRow(1, "Precio", isActive: true),
            LossReasonRow(2, "Competencia", isActive: true));

        var result = await CreateRepository(context)
            .GetAsync(new LossReasonFilter(Name: null, IsActive: null), FirstPage);

        result.IsSuccess.ShouldBeTrue();
        result.TotalCount.ShouldBe(3);
        // Without the tie-break, paging could repeat or skip the two rows sharing a name.
        result.Items.Select(x => x.Id).ShouldBe([2, 1, 3]);
    }

    [Fact]
    public async Task GetAsync_WithNameFilter_ReturnsOnlyTheRowsThatContainIt()
    {
        using var context = await CreateSeededContextAsync(
            nameof(GetAsync_WithNameFilter_ReturnsOnlyTheRowsThatContainIt),
            LossReasonRow(1, "Precio alto", isActive: true),
            LossReasonRow(2, "Competencia", isActive: true),
            LossReasonRow(3, "Tiempo", isActive: true));

        var result = await CreateRepository(context)
            .GetAsync(new LossReasonFilter("Precio", IsActive: null), FirstPage);

        result.IsSuccess.ShouldBeTrue();
        result.TotalCount.ShouldBe(1);
        result.Items.ShouldHaveSingleItem().Id.ShouldBe(1);
    }

    [Fact]
    public async Task GetAsync_WithBlankNameFilter_IgnoresIt()
    {
        using var context = await CreateSeededContextAsync(
            nameof(GetAsync_WithBlankNameFilter_IgnoresIt),
            LossReasonRow(1, "Precio", isActive: true),
            LossReasonRow(2, "Competencia", isActive: true));

        var result = await CreateRepository(context)
            .GetAsync(new LossReasonFilter("   ", IsActive: null), FirstPage);

        result.IsSuccess.ShouldBeTrue();
        result.TotalCount.ShouldBe(2);
    }

    [Fact]
    public async Task GetAsync_WithIsActiveFilter_ReturnsOnlyThatState()
    {
        using var context = await CreateSeededContextAsync(
            nameof(GetAsync_WithIsActiveFilter_ReturnsOnlyThatState),
            LossReasonRow(1, "Precio", isActive: true),
            LossReasonRow(2, "Competencia", isActive: false),
            LossReasonRow(3, "Tiempo", isActive: true));

        var result = await CreateRepository(context)
            .GetAsync(new LossReasonFilter(Name: null, IsActive: false), FirstPage);

        result.IsSuccess.ShouldBeTrue();
        result.TotalCount.ShouldBe(1);
        result.Items.ShouldHaveSingleItem().Id.ShouldBe(2);
    }

    [Fact]
    public async Task GetAsync_WithNoMatches_ReturnsAnEmptySuccessfulPage()
    {
        using var context = await CreateSeededContextAsync(
            nameof(GetAsync_WithNoMatches_ReturnsAnEmptySuccessfulPage),
            LossReasonRow(1, "Precio", isActive: true));

        var result = await CreateRepository(context)
            .GetAsync(new LossReasonFilter("Competencia", IsActive: null), FirstPage);

        result.IsSuccess.ShouldBeTrue();
        result.Items.ShouldBeEmpty();
        result.TotalCount.ShouldBe(0);
    }

    [Fact]
    public async Task GetAsync_WithASecondPage_ReturnsThatPageAndTheUnpagedTotal()
    {
        using var context = await CreateSeededContextAsync(
            nameof(GetAsync_WithASecondPage_ReturnsThatPageAndTheUnpagedTotal),
            LossReasonRow(1, "Alfa", isActive: true),
            LossReasonRow(2, "Beta", isActive: true),
            LossReasonRow(3, "Gamma", isActive: true));

        var result = await CreateRepository(context).GetAsync(
            new LossReasonFilter(Name: null, IsActive: null),
            new PageQuery(pageIndex: 1, pageSize: 2));

        result.IsSuccess.ShouldBeTrue();
        result.TotalCount.ShouldBe(3);
        result.Items.ShouldHaveSingleItem().Id.ShouldBe(3);
    }

    [Fact]
    public async Task GetAsync_WhenTheQueryFails_ReturnsPersistenceFailure()
    {
        var result = await ExecuteAgainstDisposedContextAsync(
            nameof(GetAsync_WhenTheQueryFails_ReturnsPersistenceFailure),
            repository => repository.GetAsync(new LossReasonFilter(Name: null, IsActive: null), FirstPage));

        ShouldBePersistenceFailure(result);
    }

    [Fact]
    public async Task GetAllAsync_Always_ReturnsEveryRowWithoutFiltering()
    {
        using var context = await CreateSeededContextAsync(
            nameof(GetAllAsync_Always_ReturnsEveryRowWithoutFiltering),
            LossReasonRow(1, "Precio", isActive: true),
            LossReasonRow(2, "Competencia", isActive: false));

        var result = await CreateRepository(context).GetAllAsync(FirstPage);

        result.IsSuccess.ShouldBeTrue();
        result.TotalCount.ShouldBe(2);
        result.Items.Select(x => x.Id).ShouldBe([2, 1]);
    }

    [Fact]
    public async Task CreateAsync_WithValidAggregate_CommitsAndReturnsTheGeneratedId()
    {
        using var context = CreateContext(nameof(CreateAsync_WithValidAggregate_CommitsAndReturnsTheGeneratedId));
        var aggregate = LossReasonAggregate.Create(new CreateLossReasonArgs("Precio", IsActive: true)).Value;

        var result = await CreateRepository(context).CreateAsync(aggregate);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Id.ShouldNotBe(0);
        result.Value.Name.ShouldBe("Precio");
        // Already committed: a use case creating through here does not commit again.
        var persisted = await context.LossReasons.AsNoTracking().SingleAsync();
        persisted.Name.ShouldBe("Precio");
        persisted.IsActive.ShouldBe(true);
        persisted.Id.ShouldBe(result.Value.Id);
    }

    [Fact]
    public async Task CreateAsync_WhenTheInsertFails_ReturnsPersistenceFailure()
    {
        var aggregate = LossReasonAggregate.Create(new CreateLossReasonArgs("Precio", IsActive: true)).Value;

        var result = await ExecuteAgainstDisposedContextAsync(
            nameof(CreateAsync_WhenTheInsertFails_ReturnsPersistenceFailure),
            repository => repository.CreateAsync(aggregate));

        ShouldBePersistenceFailure(result);
    }

    [Fact]
    public async Task AddAsync_WithValidAggregate_StagesTheInsertWithoutCommitting()
    {
        using var context = CreateContext(nameof(AddAsync_WithValidAggregate_StagesTheInsertWithoutCommitting));
        var aggregate = LossReasonAggregate.Create(new CreateLossReasonArgs("Precio", IsActive: true)).Value;

        var result = await CreateRepository(context).AddAsync(aggregate);

        result.IsSuccess.ShouldBeTrue();
        context.ChangeTracker.Entries<LossReasonDocument>()
            .ShouldHaveSingleItem().State.ShouldBe(EntityState.Added);
    }

    [Fact]
    public async Task AddAsync_WhenTheStagingFails_ReturnsPersistenceFailure()
    {
        var aggregate = LossReasonAggregate.Create(new CreateLossReasonArgs("Precio", IsActive: true)).Value;

        var result = await ExecuteAgainstDisposedContextAsync(
            nameof(AddAsync_WhenTheStagingFails_ReturnsPersistenceFailure),
            repository => repository.AddAsync(aggregate));

        ShouldBePersistenceFailure(result);
    }

    [Fact]
    public async Task Update_WithExistingAggregate_AddressesTheRowItCameFrom()
    {
        using var context = await CreateSeededContextAsync(
            nameof(Update_WithExistingAggregate_AddressesTheRowItCameFrom),
            LossReasonRow(7, "Precio", isActive: true));
        var aggregate = LossReasonAggregate.Reconstruct(7, "Precio", isActive: true);
        aggregate.Update(new UpdateLossReasonArgs("Competencia", IsActive: false)).IsSuccess.ShouldBeTrue();

        var result = CreateRepository(context).Update(aggregate);

        result.IsSuccess.ShouldBeTrue();
        await context.SaveChangesAsync();
        var persisted = await context.LossReasons.AsNoTracking().SingleAsync();
        persisted.Id.ShouldBe(7);
        persisted.Name.ShouldBe("Competencia");
        persisted.IsActive.ShouldBe(false);
    }

    [Fact]
    public void Update_WhenTheStagingFails_ReturnsPersistenceFailure()
    {
        var context = CreateContext(nameof(Update_WhenTheStagingFails_ReturnsPersistenceFailure));
        var repository = CreateRepository(context);
        var aggregate = LossReasonAggregate.Reconstruct(7, "Precio", isActive: true);
        context.Dispose();

        var result = repository.Update(aggregate);

        ShouldBePersistenceFailure(result);
    }

    // What RemoveAsync does to the row is covered by the integration tests: it issues a single
    // DELETE through ExecuteDeleteAsync, which the in-memory provider does not implement. Only
    // its failure branch is reachable here.
    [Fact]
    public async Task RemoveAsync_WhenTheDeleteFails_ReturnsPersistenceFailure()
    {
        var result = await ExecuteAgainstDisposedContextAsync(
            nameof(RemoveAsync_WhenTheDeleteFails_ReturnsPersistenceFailure),
            repository => repository.RemoveAsync(7));

        ShouldBePersistenceFailure(result);
    }

    private async Task<TResult> ExecuteAgainstDisposedContextAsync<TResult>(
        string databaseName,
        Func<LossReasonRepository, Task<TResult>> operation)
    {
        var context = CreateContext(databaseName);
        var repository = CreateRepository(context);
        await context.DisposeAsync().ConfigureAwait(false);

        return await operation(repository).ConfigureAwait(false);
    }

    private void ShouldBePersistenceFailure(Result result)
    {
        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Internal);
        result.Error.Origin.ShouldBe(Origin);
        _logger.ReceivedWithAnyArgs(1).Error(default, default!);
    }
}
