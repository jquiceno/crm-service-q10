namespace Infrastructure.MasterAccess.Persistence.EntityFramework.Tenants;

/// <summary>
/// Serializable snapshot of a tenant for L2 caching. The domain aggregate
/// (<see cref="Shared.Domain.Tenants.Aggregates.TenantAggregate"/>) has a private
/// constructor and cannot be deserialized by System.Text.Json, so the repository caches
/// this record and rebuilds the aggregate via <c>TenantAggregate.Reconstruct</c> on a hit.
/// </summary>
internal sealed record TenantCacheModel(string Code, string Database, int ServerDatabase);
