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
/// Exercises the repository against a real <see cref="ApplicationDbContext"/> backed by EF
/// InMemory, the same approach <c>RepositoryBaseEFTests</c> already uses for the generic base.
/// </summary>
/// <remarks>
/// What InMemory cannot promise stays out of here on purpose and lives in the integration tests:
/// the IDENTITY column, the <c>varchar</c> length, the 547 of a loss reason still in use, and the
/// case-insensitive collation of the real server — the name filter below is asserted with matching
/// case so it means the same thing under both providers.
/// </remarks>
public sealed class LossReasonRepositoryTests
{
    private const string Origin = nameof(LossReasonRepository);

    private readonly ILoggerPort<LossReasonRepository> _logger =
        Substitute.For<ILoggerPort<LossReasonRepository>>();

    private static ApplicationDbContext CreateContext(string name) =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(name)
            .Options);

    private static async Task<ApplicationDbContext> SeededContextAsync(
        string name,
        params LossReasonDocument[] documents)
    {
        var context = CreateContext(name);
        context.LossReasons.AddRange(documents);
        await context.SaveChangesAsync().ConfigureAwait(false);
        // A repository always starts from a clean change tracker: it gets a per-request context.
        context.ChangeTracker.Clear();
        return context;
    }

    private LossReasonRepository RepositoryOn(ApplicationDbContext context) => new(context, _logger);

    private static LossReasonDocument Row(int id, string? name, bool? isActive) =>
        new() { CauConsecutivoP = id, CauNombre = name, CauEstado = isActive };

    private static PageQuery FirstPage => new(pageIndex: 0, pageSize: 20);

    [Fact]
    public async Task GetByIdAsync_WithExistingRow_ReturnsMappedAggregate()
    {
        using var context = await SeededContextAsync(
            nameof(GetByIdAsync_WithExistingRow_ReturnsMappedAggregate),
            Row(7, "Precio", isActive: true));

        var result = await RepositoryOn(context).GetByIdAsync(7);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Id.ShouldBe(7);
        result.Value.Name.ShouldBe("Precio");
        result.Value.IsActive.ShouldBeTrue();
    }

    [Fact]
    public async Task GetByIdAsync_WithNullColumns_NormalizesThroughTheMapper()
    {
        using var context = await SeededContextAsync(
            nameof(GetByIdAsync_WithNullColumns_NormalizesThroughTheMapper),
            Row(7, name: null, isActive: null));

        var result = await RepositoryOn(context).GetByIdAsync(7);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Name.ShouldBe(string.Empty);
        result.Value.IsActive.ShouldBeFalse();
    }

    [Fact]
    public async Task GetByIdAsync_WithMissingRow_ReturnsNotFoundStampedWithItsOrigin()
    {
        using var context = CreateContext(nameof(GetByIdAsync_WithMissingRow_ReturnsNotFoundStampedWithItsOrigin));

        var result = await RepositoryOn(context).GetByIdAsync(404);

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.NotFound);
        result.Error.Origin.ShouldBe(Origin);
    }

    [Fact]
    public async Task GetByIdAsync_WhenTheQueryFails_ReturnsPersistenceFailure()
    {
        var result = await OnBrokenContext(nameof(GetByIdAsync_WhenTheQueryFails_ReturnsPersistenceFailure),
            repository => repository.GetByIdAsync(7));

        ShouldBePersistenceFailure(result);
    }

    [Fact]
    public async Task ExistsAsync_WithExistingRow_ReturnsTrue()
    {
        using var context = await SeededContextAsync(
            nameof(ExistsAsync_WithExistingRow_ReturnsTrue),
            Row(7, "Precio", isActive: true));

        var result = await RepositoryOn(context).ExistsAsync(7);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeTrue();
    }

    [Fact]
    public async Task ExistsAsync_WithMissingRow_ReturnsFalseInsteadOfFailing()
    {
        using var context = CreateContext(nameof(ExistsAsync_WithMissingRow_ReturnsFalseInsteadOfFailing));

        var result = await RepositoryOn(context).ExistsAsync(404);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeFalse();
    }

    [Fact]
    public async Task ExistsAsync_WhenTheQueryFails_ReturnsPersistenceFailure()
    {
        var result = await OnBrokenContext(nameof(ExistsAsync_WhenTheQueryFails_ReturnsPersistenceFailure),
            repository => repository.ExistsAsync(7));

        ShouldBePersistenceFailure(result);
    }

    [Fact]
    public async Task GetAsync_WithoutFilter_OrdersByNameAndBreaksTiesWithTheKey()
    {
        using var context = await SeededContextAsync(
            nameof(GetAsync_WithoutFilter_OrdersByNameAndBreaksTiesWithTheKey),
            Row(3, "Precio", isActive: true),
            Row(1, "Precio", isActive: true),
            Row(2, "Competencia", isActive: true));

        var result = await RepositoryOn(context)
            .GetAsync(new LossReasonFilter(Name: null, IsActive: null), FirstPage);

        result.IsSuccess.ShouldBeTrue();
        result.TotalCount.ShouldBe(3);
        // Without the tie-break by the key, OFFSET/FETCH could repeat or skip the two "Precio" rows.
        result.Items.Select(x => x.Id).ShouldBe([2, 1, 3]);
    }

    [Fact]
    public async Task GetAsync_WithNameFilter_ReturnsOnlyTheRowsThatContainIt()
    {
        using var context = await SeededContextAsync(
            nameof(GetAsync_WithNameFilter_ReturnsOnlyTheRowsThatContainIt),
            Row(1, "Precio alto", isActive: true),
            Row(2, "Competencia", isActive: true),
            Row(3, null, isActive: true));

        var result = await RepositoryOn(context)
            .GetAsync(new LossReasonFilter("Precio", IsActive: null), FirstPage);

        result.IsSuccess.ShouldBeTrue();
        result.TotalCount.ShouldBe(1);
        result.Items.ShouldHaveSingleItem().Id.ShouldBe(1);
    }

    [Fact]
    public async Task GetAsync_WithBlankNameFilter_IgnoresIt()
    {
        using var context = await SeededContextAsync(
            nameof(GetAsync_WithBlankNameFilter_IgnoresIt),
            Row(1, "Precio", isActive: true),
            Row(2, "Competencia", isActive: true));

        var result = await RepositoryOn(context)
            .GetAsync(new LossReasonFilter("   ", IsActive: null), FirstPage);

        result.IsSuccess.ShouldBeTrue();
        result.TotalCount.ShouldBe(2);
    }

    [Fact]
    public async Task GetAsync_WithIsActiveFilter_ReturnsOnlyThatState()
    {
        using var context = await SeededContextAsync(
            nameof(GetAsync_WithIsActiveFilter_ReturnsOnlyThatState),
            Row(1, "Precio", isActive: true),
            Row(2, "Competencia", isActive: false),
            Row(3, "Tiempo", isActive: null));

        var result = await RepositoryOn(context)
            .GetAsync(new LossReasonFilter(Name: null, IsActive: false), FirstPage);

        result.IsSuccess.ShouldBeTrue();
        result.TotalCount.ShouldBe(1);
        result.Items.ShouldHaveSingleItem().Id.ShouldBe(2);
    }

    [Fact]
    public async Task GetAsync_WithNoMatches_ReturnsAnEmptySuccessfulPage()
    {
        using var context = await SeededContextAsync(
            nameof(GetAsync_WithNoMatches_ReturnsAnEmptySuccessfulPage),
            Row(1, "Precio", isActive: true));

        var result = await RepositoryOn(context)
            .GetAsync(new LossReasonFilter("Competencia", IsActive: null), FirstPage);

        result.IsSuccess.ShouldBeTrue();
        result.Items.ShouldBeEmpty();
        result.TotalCount.ShouldBe(0);
    }

    [Fact]
    public async Task GetAsync_WithASecondPage_ReturnsThatPageAndTheUnpagedTotal()
    {
        using var context = await SeededContextAsync(
            nameof(GetAsync_WithASecondPage_ReturnsThatPageAndTheUnpagedTotal),
            Row(1, "Alfa", isActive: true),
            Row(2, "Beta", isActive: true),
            Row(3, "Gamma", isActive: true));

        var result = await RepositoryOn(context).GetAsync(
            new LossReasonFilter(Name: null, IsActive: null),
            new PageQuery(pageIndex: 1, pageSize: 2));

        result.IsSuccess.ShouldBeTrue();
        result.TotalCount.ShouldBe(3);
        result.Items.ShouldHaveSingleItem().Id.ShouldBe(3);
    }

    [Fact]
    public async Task GetAsync_WhenTheQueryFails_ReturnsPersistenceFailure()
    {
        var result = await OnBrokenContext(nameof(GetAsync_WhenTheQueryFails_ReturnsPersistenceFailure),
            repository => repository.GetAsync(new LossReasonFilter(Name: null, IsActive: null), FirstPage));

        ShouldBePersistenceFailure(result);
    }

    [Fact]
    public async Task GetAllAsync_Always_ReturnsEveryRowWithoutFiltering()
    {
        using var context = await SeededContextAsync(
            nameof(GetAllAsync_Always_ReturnsEveryRowWithoutFiltering),
            Row(1, "Precio", isActive: true),
            Row(2, "Competencia", isActive: false));

        var result = await RepositoryOn(context).GetAllAsync(FirstPage);

        result.IsSuccess.ShouldBeTrue();
        result.TotalCount.ShouldBe(2);
        result.Items.Select(x => x.Id).ShouldBe([2, 1]);
    }

    [Fact]
    public async Task CreateAsync_WithValidAggregate_CommitsAndReturnsTheGeneratedId()
    {
        using var context = CreateContext(nameof(CreateAsync_WithValidAggregate_CommitsAndReturnsTheGeneratedId));
        var aggregate = LossReasonAggregate.Create(new CreateLossReasonArgs("Precio", IsActive: true)).Value;

        var result = await RepositoryOn(context).CreateAsync(aggregate);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Id.ShouldNotBe(0);
        result.Value.Name.ShouldBe("Precio");
        // The insert is already committed: a use case creating through here does not commit again.
        var persisted = await context.LossReasons.AsNoTracking().SingleAsync();
        persisted.CauNombre.ShouldBe("Precio");
        persisted.CauEstado.ShouldBe(true);
        persisted.CauConsecutivoP.ShouldBe(result.Value.Id);
    }

    [Fact]
    public async Task CreateAsync_WhenTheInsertFails_ReturnsPersistenceFailure()
    {
        var aggregate = LossReasonAggregate.Create(new CreateLossReasonArgs("Precio", IsActive: true)).Value;

        var result = await OnBrokenContext(nameof(CreateAsync_WhenTheInsertFails_ReturnsPersistenceFailure),
            repository => repository.CreateAsync(aggregate));

        ShouldBePersistenceFailure(result);
    }

    [Fact]
    public async Task AddAsync_WithValidAggregate_StagesTheInsertWithoutCommitting()
    {
        using var context = CreateContext(nameof(AddAsync_WithValidAggregate_StagesTheInsertWithoutCommitting));
        var aggregate = LossReasonAggregate.Create(new CreateLossReasonArgs("Precio", IsActive: true)).Value;

        var result = await RepositoryOn(context).AddAsync(aggregate);

        result.IsSuccess.ShouldBeTrue();
        context.ChangeTracker.Entries<LossReasonDocument>()
            .ShouldHaveSingleItem().State.ShouldBe(EntityState.Added);
    }

    [Fact]
    public async Task AddAsync_WhenTheStagingFails_ReturnsPersistenceFailure()
    {
        var aggregate = LossReasonAggregate.Create(new CreateLossReasonArgs("Precio", IsActive: true)).Value;

        var result = await OnBrokenContext(nameof(AddAsync_WhenTheStagingFails_ReturnsPersistenceFailure),
            repository => repository.AddAsync(aggregate));

        ShouldBePersistenceFailure(result);
    }

    [Fact]
    public async Task Update_WithExistingAggregate_AddressesTheRowItCameFrom()
    {
        using var context = await SeededContextAsync(
            nameof(Update_WithExistingAggregate_AddressesTheRowItCameFrom),
            Row(7, "Precio", isActive: true));
        var aggregate = LossReasonAggregate.Reconstruct(7, "Precio", isActive: true);
        aggregate.Update(new UpdateLossReasonArgs("Competencia", IsActive: false)).IsSuccess.ShouldBeTrue();

        var result = RepositoryOn(context).Update(aggregate);

        result.IsSuccess.ShouldBeTrue();
        await context.SaveChangesAsync();
        var persisted = await context.LossReasons.AsNoTracking().SingleAsync();
        persisted.CauConsecutivoP.ShouldBe(7);
        persisted.CauNombre.ShouldBe("Competencia");
        persisted.CauEstado.ShouldBe(false);
    }

    [Fact]
    public void Update_WhenTheStagingFails_ReturnsPersistenceFailure()
    {
        var context = CreateContext(nameof(Update_WhenTheStagingFails_ReturnsPersistenceFailure));
        var repository = RepositoryOn(context);
        var aggregate = LossReasonAggregate.Reconstruct(7, "Precio", isActive: true);
        context.Dispose();

        var result = repository.Update(aggregate);

        ShouldBePersistenceFailure(result);
    }

    [Fact]
    public async Task RemoveAsync_WithExistingRow_StagesTheDeleteForTheUnitOfWork()
    {
        using var context = await SeededContextAsync(
            nameof(RemoveAsync_WithExistingRow_StagesTheDeleteForTheUnitOfWork),
            Row(7, "Precio", isActive: true));
        var repository = RepositoryOn(context);

        var result = await repository.RemoveAsync(7);

        result.IsSuccess.ShouldBeTrue();
        context.ChangeTracker.Entries<LossReasonDocument>()
            .ShouldHaveSingleItem().State.ShouldBe(EntityState.Deleted);
        await context.SaveChangesAsync();
        (await context.LossReasons.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task RemoveAsync_WithMissingRow_ReturnsNotFoundStampedWithItsOrigin()
    {
        using var context = CreateContext(nameof(RemoveAsync_WithMissingRow_ReturnsNotFoundStampedWithItsOrigin));

        var result = await RepositoryOn(context).RemoveAsync(404);

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.NotFound);
        result.Error.Origin.ShouldBe(Origin);
    }

    [Fact]
    public async Task RemoveAsync_WhenTheLookupFails_ReturnsPersistenceFailure()
    {
        var result = await OnBrokenContext(nameof(RemoveAsync_WhenTheLookupFails_ReturnsPersistenceFailure),
            repository => repository.RemoveAsync(7));

        ShouldBePersistenceFailure(result);
    }

    /// <summary>
    /// Runs an operation against a repository whose context was disposed underneath it, which is
    /// how the failure branch is reached without a real database.
    /// </summary>
    private async Task<T> OnBrokenContext<T>(string name, Func<LossReasonRepository, Task<T>> operation)
    {
        var context = CreateContext(name);
        var repository = RepositoryOn(context);
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
