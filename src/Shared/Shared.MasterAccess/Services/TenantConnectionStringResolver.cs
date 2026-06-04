using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace Shared.MasterAccess.Services;

public sealed class TenantConnectionStringResolver(IConfiguration configuration) : ITenantConnectionStringResolver
{
    public string Resolve(int serverDatabase, string databaseName)
    {
        var baseConnectionString = serverDatabase switch
        {
            2001 => configuration.GetConnectionString("DB-Aws1"),
            2002 => configuration.GetConnectionString("DB-Aws2"),
            2003 => configuration.GetConnectionString("DB-Aws3"),
            1007 or 3 => configuration.GetConnectionString("AzureDB"),
            _ => null
        };

        if (string.IsNullOrWhiteSpace(baseConnectionString))
            throw new InvalidOperationException(
                $"No connection string configured for server database '{serverDatabase}'.");

        return new SqlConnectionStringBuilder(baseConnectionString)
        {
            InitialCatalog = databaseName
        }.ConnectionString;
    }
}
