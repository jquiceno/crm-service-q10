using Shared.Application.Ports;

namespace Api.Session;

/// <summary>
/// Bound when multitenancy is disabled: there is a single database and no tenant to partition by,
/// so every tenant-partitioned cache skips itself instead of the port failing to resolve and
/// taking down each endpoint that depends on it.
/// </summary>
internal sealed class NoTenantCodeProvider : ITenantCodeProvider
{
    public string? Current => null;
}
