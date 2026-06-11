using Shared.Domain.Aggregates;

namespace Shared.Domain.Tenants.Aggregates;

public sealed class TenantAggregate : AggregateRoot<string>
{
    public string Code => Id;
    public string Database { get; private set; } = string.Empty;
    public int ServerDatabase { get; private set; }

    private TenantAggregate(string code, string database, int serverDatabase)
    {
        Id = code;
        Database = database;
        ServerDatabase = serverDatabase;
    }

    public static TenantAggregate Create(string code, string database, int serverDatabase)
    {
        var aggregate = new TenantAggregate(code, database, serverDatabase);
        aggregate.Created();
        return aggregate;
    }

    public static TenantAggregate Reconstruct(string code, string database, int serverDatabase) =>
        new(code, database, serverDatabase);

    protected override void Created()
    {
        SetCreatedAt(DateTime.UtcNow);
        SetUpdatedAt(DateTime.UtcNow);
    }
}
