using Xunit;

namespace IntegrationTests.Infrastructure;

[CollectionDefinition(Name)]
public sealed class IntegrationTestCollection : ICollectionFixture<SqlServerContainerFixture>
{
    public const string Name = "Integration";
}
