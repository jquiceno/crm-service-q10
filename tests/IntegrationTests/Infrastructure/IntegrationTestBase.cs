using Infrastructure.Persistence.EntityFramework;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IntegrationTests.Infrastructure;

public abstract class IntegrationTestBase : IAsyncLifetime
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
        await _fixture.DatabaseResetter.ResetAsync();
        _scope = _factory.Services.CreateScope();
    }

    public Task DisposeAsync()
    {
        _scope.Dispose();
        Client.Dispose();
        return _factory.DisposeAsync().AsTask();
    }
}
