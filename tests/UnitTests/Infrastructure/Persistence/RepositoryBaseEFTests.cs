using Infrastructure.Persistence.EntityFramework;
using Infrastructure.Persistence.EntityFramework.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using NSubstitute;
using Shared.Application.Ports;
using Shared.Domain.Aggregates;
using Shared.Domain.Pagination;
using Shared.Results.Errors;
using Shouldly;
using Xunit;

namespace UnitTests.Infrastructure.Persistence;

/// <summary>
/// RepositoryBaseEF&lt;TAggregate, TId&gt; is not currently used by any production repository:
/// every repository in this codebase maps through a dedicated infrastructure entity + mapper
/// instead of mapping a domain aggregate directly, and none of the entities registered on
/// <see cref="ApplicationDbContext"/> inherit from <see cref="AggregateRoot{TId}"/>, so the
/// generic constraint (`where TAggregate : AggregateRoot&lt;TId&gt;`) can never be satisfied by the
/// context's real model. To exercise the base class through a real ApplicationDbContext (its
/// constructor requires exactly that sealed type), a minimal test-only aggregate is registered
/// into the context's model via a custom <see cref="IModelCustomizer"/> — the standard EF Core
/// extension point for adding to a context's model without modifying the context itself.
/// </summary>
public sealed class RepositoryBaseEFTests
{
    private sealed class TestAggregate : AggregateRoot<int>
    {
        public string Name { get; set; } = string.Empty;

        private TestAggregate()
        {
        }

        public static TestAggregate Create(int id, string name)
        {
            var aggregate = new TestAggregate { Name = name };
            aggregate.Id = id;
            return aggregate;
        }

        protected override void Created()
        {
        }
    }

    // Inherits the real ModelCustomizer (rather than implementing IModelCustomizer directly) so
    // base.Customize still runs ApplicationDbContext.OnModelCreating and applies every
    // IEntityTypeConfiguration in the assembly. Replacing IModelCustomizer wholesale would skip
    // that application-model setup entirely, leaving other entities without their
    // Fluent-API-configured keys and causing model validation to fail.
    private sealed class TestModelCustomizer(ModelCustomizerDependencies dependencies) : ModelCustomizer(dependencies)
    {
        public override void Customize(ModelBuilder modelBuilder, DbContext context)
        {
            base.Customize(modelBuilder, context);

            modelBuilder.Entity<TestAggregate>(builder =>
            {
                builder.HasKey(x => x.Id);
                builder.Property(x => x.Name);
            });
        }
    }

    private sealed class TestAggregateRepository(ApplicationDbContext context, ILoggerPort<object> logger)
        : RepositoryBaseEF<TestAggregate, int>(context, logger);

    private static ApplicationDbContext CreateContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(dbName)
            .ReplaceService<IModelCustomizer, TestModelCustomizer>()
            .Options;
        return new ApplicationDbContext(options);
    }

    // ── GetByIdAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GetByIdAsync_WhenExists_ReturnsAggregate()
    {
        using var context = CreateContext(nameof(GetByIdAsync_WhenExists_ReturnsAggregate));
        context.Set<TestAggregate>().Add(TestAggregate.Create(1, "Alpha"));
        await context.SaveChangesAsync();
        var sut = new TestAggregateRepository(context, Substitute.For<ILoggerPort<object>>());

        var result = await sut.GetByIdAsync(1);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Name.ShouldBe("Alpha");
    }

    [Fact]
    public async Task GetByIdAsync_WhenNotFound_ReturnsNotFoundError()
    {
        using var context = CreateContext(nameof(GetByIdAsync_WhenNotFound_ReturnsNotFoundError));
        var sut = new TestAggregateRepository(context, Substitute.For<ILoggerPort<object>>());

        var result = await sut.GetByIdAsync(999);

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.NotFound);
        result.Error.Message.ShouldContain("999");
    }

    [Fact]
    public async Task GetByIdAsync_WhenExceptionThrown_ReturnsInternalErrorAndLogs()
    {
        var context = CreateContext(nameof(GetByIdAsync_WhenExceptionThrown_ReturnsInternalErrorAndLogs));
        var logger = Substitute.For<ILoggerPort<object>>();
        var sut = new TestAggregateRepository(context, logger);
        await context.DisposeAsync();

        var result = await sut.GetByIdAsync(1);

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Internal);
        logger.Received(1).Error(Arg.Is<Exception?>(e => e != null), Arg.Any<string>(), Arg.Any<object[]>());
    }

    // ── GetAllAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAllAsync_WhenEmpty_ReturnsEmptyPagedResult()
    {
        using var context = CreateContext(nameof(GetAllAsync_WhenEmpty_ReturnsEmptyPagedResult));
        var sut = new TestAggregateRepository(context, Substitute.For<ILoggerPort<object>>());

        var result = await sut.GetAllAsync(new PageQuery(0, 10));

        result.IsSuccess.ShouldBeTrue();
        result.Items.ShouldBeEmpty();
        result.TotalCount.ShouldBe(0);
    }

    [Fact]
    public async Task GetAllAsync_WhenMultipleItemsExist_PagesOrderedById()
    {
        using var context = CreateContext(nameof(GetAllAsync_WhenMultipleItemsExist_PagesOrderedById));
        context.Set<TestAggregate>().AddRange(
            TestAggregate.Create(3, "Gamma"),
            TestAggregate.Create(1, "Alpha"),
            TestAggregate.Create(2, "Beta"));
        await context.SaveChangesAsync();
        var sut = new TestAggregateRepository(context, Substitute.For<ILoggerPort<object>>());

        var firstPage = await sut.GetAllAsync(new PageQuery(0, 2));
        var secondPage = await sut.GetAllAsync(new PageQuery(1, 2));

        firstPage.IsSuccess.ShouldBeTrue();
        firstPage.TotalCount.ShouldBe(3);
        firstPage.Items.Select(x => x.Name).ShouldBe(["Alpha", "Beta"]);
        secondPage.IsSuccess.ShouldBeTrue();
        secondPage.Items.Select(x => x.Name).ShouldBe(["Gamma"]);
    }

    [Fact]
    public async Task GetAllAsync_WhenExceptionThrown_ReturnsInternalErrorAndLogs()
    {
        var context = CreateContext(nameof(GetAllAsync_WhenExceptionThrown_ReturnsInternalErrorAndLogs));
        var logger = Substitute.For<ILoggerPort<object>>();
        var sut = new TestAggregateRepository(context, logger);
        await context.DisposeAsync();

        var result = await sut.GetAllAsync(new PageQuery(0, 10));

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Internal);
        logger.Received(1).Error(Arg.Is<Exception?>(e => e != null), Arg.Any<string>(), Arg.Any<object[]>());
    }

    // ── AddAsync ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task AddAsync_WhenValid_PersistsToContext()
    {
        using var context = CreateContext(nameof(AddAsync_WhenValid_PersistsToContext));
        var sut = new TestAggregateRepository(context, Substitute.For<ILoggerPort<object>>());

        var result = await sut.AddAsync(TestAggregate.Create(1, "Alpha"));
        await context.SaveChangesAsync();

        result.IsSuccess.ShouldBeTrue();
        (await context.Set<TestAggregate>().CountAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task AddAsync_WhenExceptionThrown_ReturnsInternalErrorAndLogs()
    {
        var context = CreateContext(nameof(AddAsync_WhenExceptionThrown_ReturnsInternalErrorAndLogs));
        var logger = Substitute.For<ILoggerPort<object>>();
        var sut = new TestAggregateRepository(context, logger);
        await context.DisposeAsync();

        var result = await sut.AddAsync(TestAggregate.Create(1, "Alpha"));

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Internal);
        logger.Received(1).Error(Arg.Is<Exception?>(e => e != null), Arg.Any<string>(), Arg.Any<object[]>());
    }

    // ── Update ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Update_WhenValid_PersistsChanges()
    {
        const string dbName = nameof(Update_WhenValid_PersistsChanges);
        using (var seedCtx = CreateContext(dbName))
        {
            seedCtx.Set<TestAggregate>().Add(TestAggregate.Create(1, "Original"));
            await seedCtx.SaveChangesAsync();
        }

        using var context = CreateContext(dbName);
        var sut = new TestAggregateRepository(context, Substitute.For<ILoggerPort<object>>());

        var result = sut.Update(TestAggregate.Create(1, "Updated"));
        await context.SaveChangesAsync();

        result.IsSuccess.ShouldBeTrue();
        using var verifyCtx = CreateContext(dbName);
        (await verifyCtx.Set<TestAggregate>().SingleAsync()).Name.ShouldBe("Updated");
    }

    [Fact]
    public async Task Update_WhenExceptionThrown_ReturnsInternalErrorAndLogs()
    {
        var context = CreateContext(nameof(Update_WhenExceptionThrown_ReturnsInternalErrorAndLogs));
        var logger = Substitute.For<ILoggerPort<object>>();
        var sut = new TestAggregateRepository(context, logger);
        await context.DisposeAsync();

        var result = sut.Update(TestAggregate.Create(1, "Updated"));

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Internal);
        logger.Received(1).Error(Arg.Is<Exception?>(e => e != null), Arg.Any<string>(), Arg.Any<object[]>());
    }

    // ── Remove ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Remove_WhenValid_RemovesFromContext()
    {
        using var context = CreateContext(nameof(Remove_WhenValid_RemovesFromContext));
        var tracked = TestAggregate.Create(1, "Alpha");
        context.Set<TestAggregate>().Add(tracked);
        await context.SaveChangesAsync();
        var sut = new TestAggregateRepository(context, Substitute.For<ILoggerPort<object>>());

        var result = sut.Remove(tracked);
        await context.SaveChangesAsync();

        result.IsSuccess.ShouldBeTrue();
        (await context.Set<TestAggregate>().CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task Remove_WhenExceptionThrown_ReturnsInternalErrorAndLogs()
    {
        var context = CreateContext(nameof(Remove_WhenExceptionThrown_ReturnsInternalErrorAndLogs));
        var logger = Substitute.For<ILoggerPort<object>>();
        var sut = new TestAggregateRepository(context, logger);
        await context.DisposeAsync();

        var result = sut.Remove(TestAggregate.Create(1, "Alpha"));

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Internal);
        logger.Received(1).Error(Arg.Is<Exception?>(e => e != null), Arg.Any<string>(), Arg.Any<object[]>());
    }
}
