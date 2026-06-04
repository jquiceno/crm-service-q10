using Shared.Domain.Tenants.Aggregates;
using Shared.MasterAccess.Persistence.EntityFramework.Tenants.Entities;

namespace Shared.MasterAccess.Persistence.EntityFramework.Tenants.Mappers;

public static class TenantRepositoryMapper
{
    public static TenantAggregate ToDomain(Tenant document) =>
        TenantAggregate.Reconstruct(document.Code, document.Database, document.ServerDatabase);
}
