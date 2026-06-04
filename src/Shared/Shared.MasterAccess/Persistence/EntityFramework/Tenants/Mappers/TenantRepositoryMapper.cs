using Announcement.Domain.Tenants.Aggregates;
using Shared.MasterAccess.Persistence.EntityFramework.Tenants.Entities;

namespace Shared.MasterAccess.Persistence.EntityFramework.Tenants.Mappers;

public static class TenantRepositoryMapper
{
    public static TenantAggregate ToDomain(Tenant document) =>
        TenantAggregate.Reconstruct(document.Code, document.Database, document.ServerDatabase);

    public static Tenant ToDocument(TenantAggregate aggregate) =>
        new() { Code = aggregate.Code, Database = aggregate.Database, ServerDatabase = aggregate.ServerDatabase };
}
