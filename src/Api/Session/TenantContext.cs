using Shared.Application.Ports;

namespace Api.Session;

/// <summary>
/// Scoped holder for the resolved tenant's database connection string. A single instance per request
/// satisfies both the write side (<see cref="ITenantConnectionInitializer"/>, filled by the tenant
/// middleware) and the read side (<see cref="IDbConnectionProvider"/>, read by the per-tenant
/// <c>DbContext</c>). It is never registered or injected as the concrete type — only bound once behind
/// its interfaces in the composition root.
/// </summary>
public sealed class TenantContext : IDbConnectionProvider, ITenantConnectionInitializer
{
    private string? _connectionString;

    /// <summary>
    /// The tenant connection string. Throws if read before the tenant has been resolved — this
    /// guards routes that reach persistence without passing through tenant resolution (e.g. an
    /// excluded path), turning a cryptic SqlClient failure into a clear invariant violation.
    /// </summary>
    public string ConnectionString =>
        _connectionString
        ?? throw new InvalidOperationException(
            "The tenant has not been resolved for this request, so no connection string is available. "
            + "Ensure the request carries a tenant code and is not on a tenant-excluded path.");

    public void Initialize(string connectionString) => _connectionString = connectionString;
}
