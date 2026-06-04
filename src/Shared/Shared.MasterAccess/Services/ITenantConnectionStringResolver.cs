namespace Shared.MasterAccess.Services;

public interface ITenantConnectionStringResolver
{
    string Resolve(int serverDatabase, string databaseName);
}
