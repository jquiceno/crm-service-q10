using Infrastructure.Adapters.Persistence;
using Infrastructure.Persistence.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using NSubstitute;
using Shared.Application.Ports;
using Shared.Results.Errors;
using Shouldly;
using Xunit;

namespace UnitTests.Infrastructure.Persistence;

public sealed class UnitOfWorkAdapterTests
{
    // The template's ApplicationDbContext has no bounded-context entities registered yet, so a
    // minimal test-only entity is registered via a custom IModelCustomizer to exercise
    // SaveChangesAsync through a real persisted row, mirroring the approach used in
    // RepositoryBaseEFTests / ApplicationDbContextTests.
    private sealed class TestEntity
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    private sealed class TestModelCustomizer(ModelCustomizerDependencies dependencies) : ModelCustomizer(dependencies)
    {
        public override void Customize(ModelBuilder modelBuilder, DbContext context)
        {
            base.Customize(modelBuilder, context);

            modelBuilder.Entity<TestEntity>(builder =>
            {
                builder.HasKey(x => x.Id);
                builder.Property(x => x.Name);
            });
        }
    }

    private static ApplicationDbContext CreateContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(dbName)
            .ReplaceService<IModelCustomizer, TestModelCustomizer>()
            .Options;
        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task CommitAsync_WhenPendingChangesExist_PersistsAndReturnsSuccess()
    {
        using var context = CreateContext(nameof(CommitAsync_WhenPendingChangesExist_PersistsAndReturnsSuccess));
        context.Set<TestEntity>().Add(new TestEntity { Id = 1, Name = "Pending" });
        var sut = new UnitOfWorkAdapter(context, Substitute.For<ILoggerPort<UnitOfWorkAdapter>>());

        var result = await sut.CommitAsync();

        result.IsSuccess.ShouldBeTrue();
        (await context.Set<TestEntity>().CountAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task CommitAsync_WhenNoPendingChanges_ReturnsSuccess()
    {
        using var context = CreateContext(nameof(CommitAsync_WhenNoPendingChanges_ReturnsSuccess));
        var sut = new UnitOfWorkAdapter(context, Substitute.For<ILoggerPort<UnitOfWorkAdapter>>());

        var result = await sut.CommitAsync();

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task CommitAsync_WhenIdCollidesWithExistingRow_ReturnsClassifiedErrorAndLogs()
    {
        const string dbName = nameof(CommitAsync_WhenIdCollidesWithExistingRow_ReturnsClassifiedErrorAndLogs);
        using (var seedCtx = CreateContext(dbName))
        {
            seedCtx.Set<TestEntity>().Add(new TestEntity { Id = 1, Name = "Existing" });
            await seedCtx.SaveChangesAsync();
        }

        using var context = CreateContext(dbName);
        context.Set<TestEntity>().Add(new TestEntity { Id = 1, Name = "Duplicate" });
        var logger = Substitute.For<ILoggerPort<UnitOfWorkAdapter>>();
        var sut = new UnitOfWorkAdapter(context, logger);

        // Adding a second row with the same explicit primary key as an already-persisted row
        // produces a genuine DbUpdateException on SaveChanges, handled by the dedicated
        // catch(DbUpdateException) branch.
        var result = await sut.CommitAsync();

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Internal);
        logger.Received(1).Error(Arg.Is<Exception?>(e => e != null), Arg.Any<string>(), Arg.Any<object[]>());
    }

    [Fact]
    public async Task CommitAsync_WhenExceptionThrown_ReturnsInternalErrorAndLogs()
    {
        var context = CreateContext(nameof(CommitAsync_WhenExceptionThrown_ReturnsInternalErrorAndLogs));
        var logger = Substitute.For<ILoggerPort<UnitOfWorkAdapter>>();
        var sut = new UnitOfWorkAdapter(context, logger);
        await context.DisposeAsync();

        var result = await sut.CommitAsync();

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Internal);
        logger.Received(1).Error(Arg.Is<Exception?>(e => e != null), Arg.Any<string>(), Arg.Any<object[]>());
    }
}
