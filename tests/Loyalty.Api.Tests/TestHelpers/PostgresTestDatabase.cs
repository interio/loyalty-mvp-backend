using System;
using System.Threading.Tasks;
using Npgsql;

namespace Loyalty.Api.Tests.TestHelpers;

public sealed class PostgresTestDatabase : IAsyncDisposable
{
    private readonly string _adminConnectionString;
    private readonly string _databaseName;

    public string ConnectionString { get; }

    private PostgresTestDatabase(string adminConnectionString, string databaseName, string connectionString)
    {
        _adminConnectionString = adminConnectionString;
        _databaseName = databaseName;
        ConnectionString = connectionString;
    }

    public static async Task<PostgresTestDatabase> CreateAsync()
    {
        var baseConnection = Environment.GetEnvironmentVariable("TEST_CONNECTIONSTRING")
            ?? "Host=postgres;Port=5432;Database=postgres;Username=loyalty;Password=loyalty";

        var builder = new NpgsqlConnectionStringBuilder(baseConnection);
        var databaseName = $"loyalty_test_{Guid.NewGuid():N}";

        var adminBuilder = new NpgsqlConnectionStringBuilder(builder.ConnectionString)
        {
            Database = "postgres"
        };

        await using (var conn = new NpgsqlConnection(adminBuilder.ConnectionString))
        {
            await conn.OpenAsync();
            await using var cmd = new NpgsqlCommand($"CREATE DATABASE \"{databaseName}\"", conn);
            await cmd.ExecuteNonQueryAsync();
        }

        builder.Database = databaseName;
        return new PostgresTestDatabase(adminBuilder.ConnectionString, databaseName, builder.ConnectionString);
    }

    public async ValueTask DisposeAsync()
    {
        await using var conn = new NpgsqlConnection(_adminConnectionString);
        await conn.OpenAsync();

        await using (var terminate = new NpgsqlCommand(
                         "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = @db",
                         conn))
        {
            terminate.Parameters.AddWithValue("db", _databaseName);
            await terminate.ExecuteNonQueryAsync();
        }

        await using var cmd = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{_databaseName}\"", conn);
        await cmd.ExecuteNonQueryAsync();
    }
}
