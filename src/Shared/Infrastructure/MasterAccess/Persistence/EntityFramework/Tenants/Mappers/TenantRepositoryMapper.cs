using Infrastructure.MasterAccess.Persistence.EntityFramework.Tenants.Entities;
using Shared.Domain.Tenants.Aggregates;

namespace Infrastructure.MasterAccess.Persistence.EntityFramework.Tenants.Mappers;

public static class TenantRepositoryMapper
{
    public static TenantAggregate ToDomain(Tenant document) =>
        TenantAggregate.Reconstruct(document.Code, document.Database, document.ServerDatabase);
}
