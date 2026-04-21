using Infrastructure.Persistence.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;
using Xunit;

namespace IntegrationTests.Infrastructure;

public sealed class SqlServerContainerFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _container = new MsSqlBuilder()
        .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public DatabaseResetter DatabaseResetter { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(ConnectionString)
            .Options;

        await using var dbContext = new ApplicationDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();

        DatabaseResetter = new DatabaseResetter(ConnectionString);
        await DatabaseResetter.InitializeAsync();
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }
}
