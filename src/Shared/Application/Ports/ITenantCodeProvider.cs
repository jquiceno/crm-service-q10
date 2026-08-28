namespace Shared.Application.Ports;

/// <summary>
/// Exposes the tenant code of the current request to the layers below HTTP, so a cache entry can be
/// partitioned per tenant without dragging the web layer into persistence. It is the read side of
/// what the tenant middleware records, mirroring <see cref="IDbConnectionProvider"/>.
/// <para>
/// <see cref="Current"/> is <c>null</c> when the request carries no resolved tenant — multitenancy
/// disabled, or a tenant-excluded path. Callers must read that as "there is nothing to partition
/// by", and therefore skip the cache rather than fall back to a shared key.
/// </para>
/// </summary>
public interface ITenantCodeProvider
{
    string? Current { get; }
}
