using Shared.Results;

namespace Infrastructure.MasterAccess.Resolvers;

public interface ITenantConnectionStringResolver
{
    Result<string> Resolve(int serverDatabase, string databaseName);
}
