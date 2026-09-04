using Infrastructure.Persistence.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Shouldly;
using Xunit;

namespace UnitTests.Infrastructure.Persistence;

public sealed class ApplicationDbContextTests
{
    // A minimal test-only entity is registered via a custom IModelCustomizer — the standard EF
    // Core extension point — so SaveChangesAsync can be exercised through a real persisted row
    // without coupling this context-agnostic test to any bounded context's entity.
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
            .Options;
        return new ApplicationDbContext(options);
    }

    private static ApplicationDbContext CreateContextWithTestEntity(string dbName)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(dbName)
            .ReplaceService<IModelCustomizer, TestModelCustomizer>()
            .Options;
        return new ApplicationDbContext(options);
    }

    [Fact]
    public void Constructor_WithInMemoryOptions_UsesInMemoryProvider()
    {
        using var context = CreateContext(nameof(Constructor_WithInMemoryOptions_UsesInMemoryProvider));

        var providerName = context.Database.ProviderName;
        providerName.ShouldNotBeNull();
        providerName.ShouldContain("InMemory");
    }

    [Fact]
    public void OnModelCreating_AppliesTheAssemblyConfigurations_WithoutThrowing()
    {
        using var context = CreateContext(nameof(OnModelCreating_AppliesTheAssemblyConfigurations_WithoutThrowing));

        context.Model.GetEntityTypes().ShouldNotBeEmpty();
    }

    [Fact]
    public async Task SaveChangesAsync_WhenChangesArePending_ReturnsAffectedRowCount()
    {
        using var context = CreateContextWithTestEntity(nameof(SaveChangesAsync_WhenChangesArePending_ReturnsAffectedRowCount));
        context.Set<TestEntity>().Add(new TestEntity { Id = 1, Name = "Alpha" });

        var affected = await context.SaveChangesAsync();

        affected.ShouldBe(1);
        (await context.Set<TestEntity>().CountAsync()).ShouldBe(1);
    }
}
