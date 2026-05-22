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
        ArgumentNullException.ThrowIfNull(_connectionString);
        var connection = new SqlConnection(_connectionString);
        await using (connection.ConfigureAwait(false))
        {
            await connection.OpenAsync().ConfigureAwait(false);
            _respawner = await Respawner.CreateAsync(connection, new RespawnerOptions
            {
                DbAdapter = DbAdapter.SqlServer
            }).ConfigureAwait(false);
        }
    }

    public async Task ResetAsync()
    {
        if (_respawner is null)
            throw new InvalidOperationException(
                "DatabaseResetter not initialized. Call InitializeAsync() first.");

        var connection = new SqlConnection(_connectionString);
        await using (connection.ConfigureAwait(false))
        {
            await connection.OpenAsync().ConfigureAwait(false);
            await _respawner.ResetAsync(connection).ConfigureAwait(false);
        }
    }
}
