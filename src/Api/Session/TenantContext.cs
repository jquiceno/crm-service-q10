using Shared.Application.Ports;

namespace Api.Session;

/// <summary>
/// Scoped holder for what the tenant middleware resolved for the current request. A single instance
/// serves the write side (<see cref="ITenantConnectionInitializer"/>) and the two read sides —
/// <see cref="IDbConnectionProvider"/> for the per-tenant <c>DbContext</c> and
/// <see cref="ITenantCodeProvider"/> for the tenant-partitioned cache keys. It is never registered
/// or injected as the concrete type — only bound behind its interfaces in the composition root.
/// </summary>
public sealed class TenantContext : IDbConnectionProvider, ITenantCodeProvider, ITenantConnectionInitializer
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

    /// <summary>
    /// The resolved tenant code, or <c>null</c> before resolution. Unlike
    /// <see cref="ConnectionString"/> it does not throw: a caller that only wants to partition a
    /// cache key must be able to ask and get "no tenant" as an answer.
    /// </summary>
    public string? Current { get; private set; }

    public void Initialize(string connectionString, string entityCode)
    {
        _connectionString = connectionString;
        Current = entityCode;
    }
}
