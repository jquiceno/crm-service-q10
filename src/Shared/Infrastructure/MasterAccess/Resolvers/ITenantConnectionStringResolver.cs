using Shared.Results;

namespace Infraestructure.MasterAccess.Resolvers;

public interface ITenantConnectionStringResolver
{
    Result<string> Resolve(int serverDatabase, string databaseName);
}
