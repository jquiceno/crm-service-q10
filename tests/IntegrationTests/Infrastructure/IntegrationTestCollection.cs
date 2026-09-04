using IntegrationTests.Caching;
using Xunit;

namespace IntegrationTests.Infrastructure;

/// <summary>
/// The containers every integration test boots the app against: SQL Server for persistence and
/// Redis for the L2 cache, which multitenancy requires at startup.
/// </summary>
[CollectionDefinition(Name)]
public sealed class IntegrationTestCollection
    : ICollectionFixture<SqlServerContainerFixture>, ICollectionFixture<RedisContainerFixture>
{
    public const string Name = "Integration";
}
