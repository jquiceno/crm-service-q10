using Shared.Domain.Entities;

namespace Shared.Domain.Tenants.Entities;

public sealed class TenantEntity : EntityRoot<string>
{
    public string Database { get; private set; }
    public int ServerDatabase { get; private set; }

    internal TenantEntity(string code, string database, int serverDatabase)
    {
        Id = code;
        Database = database;
        ServerDatabase = serverDatabase;
    }
}
