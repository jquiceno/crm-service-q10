using Shared.Domain.Aggregates;
using Shared.Domain.Tenants.Entities;

namespace Shared.Domain.Tenants.Aggregates;

public sealed class TenantAggregate : AggregateRoot<TenantEntity, string>
{
    private TenantAggregate(TenantEntity entity) : base(entity) { }

    public string Code => Id;
    public string Database => Entity.Database;
    public int ServerDatabase => Entity.ServerDatabase;

    public static TenantAggregate Reconstruct(string code, string database, int serverDatabase) =>
        new(new TenantEntity(code, database, serverDatabase));
}
