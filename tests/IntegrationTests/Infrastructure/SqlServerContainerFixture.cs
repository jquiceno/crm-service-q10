using DotNet.Testcontainers.Builders;
using Infrastructure.Persistence.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;
using Xunit;

namespace IntegrationTests.Infrastructure;

public sealed class SqlServerContainerFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _container = new MsSqlBuilder("mcr.microsoft.com/azure-sql-edge:latest")
        .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(1433))
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public DatabaseResetter DatabaseResetter { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync().ConfigureAwait(false);

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(ConnectionString)
            .Options;

        using var dbContext = new ApplicationDbContext(options);
        await dbContext.Database.EnsureCreatedAsync().ConfigureAwait(false);

        DatabaseResetter = new DatabaseResetter(ConnectionString);
        await DatabaseResetter.InitializeAsync().ConfigureAwait(false);
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync().ConfigureAwait(false);
    }
}
