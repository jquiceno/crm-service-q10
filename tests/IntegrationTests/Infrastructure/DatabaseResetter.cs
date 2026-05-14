using Microsoft.Data.SqlClient;
using Respawn;

namespace IntegrationTests.Infrastructure;

public sealed class DatabaseResetter
{
    private readonly string _connectionString;
    private Respawner? _respawner;

    public DatabaseResetter(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task InitializeAsync()
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        _respawner = await Respawner.CreateAsync(connection, new RespawnerOptions
        {
            DbAdapter = DbAdapter.SqlServer
        });
    }

    public async Task ResetAsync()
    {
        if (_respawner is null)
            throw new InvalidOperationException(
                "DatabaseResetter not initialized. Call InitializeAsync() first.");

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await _respawner.ResetAsync(connection);
    }
}
