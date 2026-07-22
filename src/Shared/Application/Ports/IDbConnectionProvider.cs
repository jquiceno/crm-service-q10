namespace Shared.Application.Ports;

/// <summary>
/// Supplies the database connection string for the current unit of work. In multitenant mode the
/// value is resolved per request (from the tenant-info endpoint); the persistence layer reads it
/// when the <c>DbContext</c> is constructed, so the abstraction lets Infrastructure depend on a port
/// instead of the concrete web-layer session object.
/// </summary>
public interface IDbConnectionProvider
{
    string ConnectionString { get; }
}
