using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Shared.Results;
using Shared.Results.Errors;

namespace Infraestructure.MasterAccess.Resolvers;

public sealed class TenantConnectionStringResolver(IConfiguration configuration) : ITenantConnectionStringResolver
{
    public Result<string> Resolve(int serverDatabase, string databaseName)
    {
        var serverMapping = configuration
            .GetSection("TenantServers")
            .GetChildren()
            .ToDictionary(c => c.Key, c => c.Value ?? string.Empty);

        // Attempt to retrieve the connection string for the server.
        serverMapping.TryGetValue(serverDatabase.ToString(), out var connectionString);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return new DomainError($"No connection string configured for server database '{serverDatabase}'.", ErrorType.Internal);
        }

        return new SqlConnectionStringBuilder(connectionString)
        {
            InitialCatalog = databaseName
        }.ConnectionString;
    }
}
