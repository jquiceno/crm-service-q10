using IntegrationTests.Caching;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace IntegrationTests.Infrastructure;

/// <summary>
/// Guards the suite against testing nothing real. The app knows one persistence mode only — SQL
/// Server, connection string supplied by the resolved tenant — and <c>ApiFactory</c> feeds it the
/// Testcontainers database through a stubbed resolver. If that wiring ever stops taking effect the
/// rest of the suite could keep passing against something that is not a real database. This test
/// fails instead.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public sealed class PersistenceProviderTests : IntegrationTestBase
{
    public PersistenceProviderTests(SqlServerContainerFixture fixture, RedisContainerFixture cache)
        : base(fixture, cache) { }

    [Fact]
    public void ApplicationDbContext_Resolves_To_SqlServer_Provider()
    {
        Db.Database.ProviderName.ShouldBe("Microsoft.EntityFrameworkCore.SqlServer");
    }

    /// <summary>
    /// Runs raw SQL against the container. Unlike <c>CanConnectAsync</c> — which returns <c>true</c>
    /// under a non-relational provider too, and so distinguishes nothing — this one is relational
    /// and only succeeds against a real SQL Server.
    /// </summary>
    [Fact]
    public async Task ApplicationDbContext_Executes_Relational_Sql_Against_The_Container()
    {
        await Should.NotThrowAsync(() => Db.Database.ExecuteSqlRawAsync("SELECT 1"));
    }
}
