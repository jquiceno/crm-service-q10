using Infrastructure.Persistence.EntityFramework;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IntegrationTests.Infrastructure;

public abstract class IntegrationTestBase : IAsyncLifetime, IAsyncDisposable
{
    private readonly SqlServerContainerFixture _fixture;
    private readonly ApiFactory _factory;
    private IServiceScope _scope = null!;

    protected HttpClient Client { get; }
    protected ApplicationDbContext Db => _scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

    protected IntegrationTestBase(SqlServerContainerFixture fixture)
    {
        _fixture = fixture;
        _factory = new ApiFactory(fixture.ConnectionString);
        Client = _factory.CreateClient();
    }

    public async Task InitializeAsync()
    {
        await _fixture.DatabaseResetter.ResetAsync().ConfigureAwait(false);
        _scope = _factory.Services.CreateScope();
    }

    Task IAsyncLifetime.DisposeAsync() => DisposeAsync().AsTask();

    public async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        _scope.Dispose();
        Client.Dispose();
        await _factory.DisposeAsync().ConfigureAwait(false);
    }
}
