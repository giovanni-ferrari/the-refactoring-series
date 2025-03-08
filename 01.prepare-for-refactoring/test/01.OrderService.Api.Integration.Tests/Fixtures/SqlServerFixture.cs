using Microsoft.Data.SqlClient;
using Testcontainers.MsSql;

namespace OrderService.Api.Integration.Tests.Fixtures;

/// <summary>
/// Represents a fixture that provides a SQL Server instance for testing.
/// Startup SQL Server docker container, create a database, and run migrations.
/// </summary>
public class SqlServerFixture : IFixture, IAsyncDisposable
{
    private readonly MsSqlContainer _sqlContainer;

    public SqlServerFixture()
    {
        _sqlContainer = new MsSqlBuilder()
            .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
            .WithPassword("yourStrong(!)Password")
            .Build();
    }

    public string GetConnectionString()
    {
        SqlConnectionStringBuilder builder = new(_sqlContainer.GetConnectionString());
        builder.InitialCatalog = "OrderService";
        return builder.ConnectionString;
    }

    public async Task StartAsync()
    {
        await _sqlContainer.StartAsync();
        await InitializeDatabaseAsync();
    }

    public string ConnectionString => _sqlContainer.GetConnectionString();

    private async Task InitializeDatabaseAsync()
    {
        var scriptPath = Path.Combine(AppContext.BaseDirectory, "order-service.sql");
        var script = await File.ReadAllTextAsync(scriptPath);

        // Split the script on GO statements
        var batches = script
            .Split(new[] { "GO" }, StringSplitOptions.RemoveEmptyEntries)
            .Where(batch => !string.IsNullOrWhiteSpace(batch));

        using (var connection = new SqlConnection(ConnectionString))
        {
            await connection.OpenAsync();
            foreach (var batch in batches)
            {
                using (var command = new SqlCommand(batch, connection))
                {
                    await command.ExecuteNonQueryAsync();
                }
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _sqlContainer.StopAsync();
        await _sqlContainer.DisposeAsync();
    }
}
