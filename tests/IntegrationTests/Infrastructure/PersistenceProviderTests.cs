using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace IntegrationTests.Infrastructure;

/// <summary>
/// Guards the integration suite against silently falling back to the in-memory provider. The app
/// boots with multitenancy off (its default is in-memory) and <c>ApiFactory</c> re-points the
/// <c>DbContext</c> at the Testcontainers SQL Server; if that override ever stops taking effect the
/// rest of the suite would keep passing while testing nothing real. This test fails instead.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public sealed class PersistenceProviderTests : IntegrationTestBase
{
    public PersistenceProviderTests(SqlServerContainerFixture fixture) : base(fixture) { }

    [Fact]
    public void ApplicationDbContext_Resolves_To_SqlServer_Provider()
    {
        Db.Database.ProviderName.ShouldBe("Microsoft.EntityFrameworkCore.SqlServer");
    }

    /// <summary>
    /// Runs raw SQL against the container. Unlike <c>CanConnectAsync</c> — which returns <c>true</c>
    /// under the in-memory provider too, and so distinguishes nothing — this one is relational: on
    /// in-memory it throws <see cref="InvalidOperationException"/> before any network call.
    /// </summary>
    [Fact]
    public async Task ApplicationDbContext_Executes_Relational_Sql_Against_The_Container()
    {
        await Should.NotThrowAsync(() => Db.Database.ExecuteSqlRawAsync("SELECT 1"));
    }
}
