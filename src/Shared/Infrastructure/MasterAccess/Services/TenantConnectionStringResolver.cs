using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace Infraestructure.MasterAccess.Services;

public sealed class TenantConnectionStringResolver(IConfiguration configuration) : ITenantConnectionStringResolver
{
    public string Resolve(int serverDatabase, string databaseName)
    {
        var serverMapping = configuration
            .GetSection("TenantServers")
            .GetChildren()
            .ToDictionary(c => c.Key, c => c.Value ?? string.Empty);

        // Intenta obtener el nombre de la cadena de conexión para el servidor.
        serverMapping.TryGetValue(serverDatabase.ToString(), out var connectionStringName);

        if (string.IsNullOrWhiteSpace(connectionStringName))
        {
            throw new InvalidOperationException($"No connection string configured for server database '{serverDatabase}'.");
        }

        return new SqlConnectionStringBuilder(connectionStringName)
        {
            InitialCatalog = databaseName
        }.ConnectionString;
    }
}
