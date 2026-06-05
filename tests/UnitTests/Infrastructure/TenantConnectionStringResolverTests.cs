using Infrastructure.MasterAccess.Resolvers;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Shared.Results.Errors;
using Shouldly;
using Xunit;

namespace UnitTests.Infrastructure;

public sealed class TenantConnectionStringResolverTests
{
    private static IConfiguration CreateConfig(params (string key, string value)[] entries) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(entries.Select(e =>
                new KeyValuePair<string, string?>($"TenantServers:{e.key}", e.value)))
            .Build();

    [Theory]
    [InlineData(99, null)]
    [InlineData(1, "")]
    [InlineData(1, "   ")]
    public void Resolve_WhenConnectionStringMissingOrBlank_ReturnsInternalError(
        int serverKey, string? configuredValue)
    {
        var config = configuredValue is null
            ? CreateConfig()
            : CreateConfig(("1", configuredValue));
        var sut = new TenantConnectionStringResolver(config);

        var result = sut.Resolve(serverKey, "tenant_db");

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Internal);
        result.Error.Message.ShouldContain(serverKey.ToString());
    }

    [Fact]
    public void Resolve_WhenValidConfig_ReturnsConnectionStringWithCatalogOverridden()
    {
        var sut = new TenantConnectionStringResolver(
            CreateConfig(("1", "Data Source=myServer;Initial Catalog=master;")));

        var result = sut.Resolve(1, "tenant_db");

        result.IsSuccess.ShouldBeTrue();
        new SqlConnectionStringBuilder(result.Value).InitialCatalog.ShouldBe("tenant_db");
        result.Value.ShouldContain("myServer");
    }

    [Fact]
    public void Resolve_WhenMultipleServersConfigured_ReturnsConnectionStringForMatchingServer()
    {
        var sut = new TenantConnectionStringResolver(CreateConfig(
            ("1", "Data Source=server1;Initial Catalog=master;"),
            ("2", "Data Source=server2;Initial Catalog=master;")));

        var result = sut.Resolve(2, "tenant_db");

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldContain("server2");
        result.Value.ShouldNotContain("server1");
    }

    [Fact]
    public void Resolve_WhenValidConfig_DatabaseNameReplacesOriginalCatalog()
    {
        var sut = new TenantConnectionStringResolver(
            CreateConfig(("1", "Data Source=myServer;Initial Catalog=original_db;")));

        var result = sut.Resolve(1, "new_db");

        result.IsSuccess.ShouldBeTrue();
        new SqlConnectionStringBuilder(result.Value).InitialCatalog.ShouldBe("new_db");
    }
}
