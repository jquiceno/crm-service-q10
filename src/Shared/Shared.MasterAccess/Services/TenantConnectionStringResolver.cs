using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace Shared.MasterAccess.Services;

public sealed class TenantConnectionStringResolver(IConfiguration configuration) : ITenantConnectionStringResolver
{
    public string Resolve(int serverDatabase, string databaseName)
    {
        var serverMapping = configuration
            .GetSection("TenantServers")
            .GetChildren()
            .ToDictionary(c => c.Key, c => c.Value ?? string.Empty);

        if (!serverMapping.TryGetValue(serverDatabase.ToString(), out var connectionStringKey)
            || string.IsNullOrWhiteSpace(connectionStringKey))
        {
            throw new InvalidOperationException(
                $"No connection string configured for server database '{serverDatabase}'.");
        }

        return new SqlConnectionStringBuilder(connectionStringKey)
        {
            InitialCatalog = databaseName
        }.ConnectionString;
    }
}
