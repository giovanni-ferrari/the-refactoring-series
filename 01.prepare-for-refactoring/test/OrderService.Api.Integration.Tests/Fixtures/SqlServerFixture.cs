using Microsoft.Data.SqlClient;
using Testcontainers.MsSql;

namespace OrderService.Api.Integration.Tests.Fixtures;

/// <summary>
/// Represents a fixture that provides a SQL Server instance for testing.
/// Startup SQL Server docker container, create a database, and run migrations.
/// </summary>
public class SqlServerFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _sqlContainer;
    private const string DATABASE_PASSWORD = "yourStrong(!)Password";
    private const string DATABASE_IMAGE = "mcr.microsoft.com/mssql/server:2022-latest";
    private const string DATABASE_NAME = "OrderService";
    private const string DATABASE_CREATE_SCRIPT = "order-service.sql";

    public SqlServerFixture()
    {
        _sqlContainer = new MsSqlBuilder()
            .WithImage(DATABASE_IMAGE)
            .WithPassword(DATABASE_PASSWORD)
            .Build();
    }

    public string GetConnectionString()
    {
        SqlConnectionStringBuilder builder = new(_sqlContainer.GetConnectionString());
        builder.InitialCatalog = DATABASE_NAME;
        return builder.ConnectionString;
    }

    public async Task InitializeAsync()
    {
        await _sqlContainer.StartAsync();
        await InitializeDatabaseAsync();
    }

    private async Task InitializeDatabaseAsync()
    {
        var scriptPath = Path.Combine(AppContext.BaseDirectory, DATABASE_CREATE_SCRIPT);
        var script = await File.ReadAllTextAsync(scriptPath);

        // Split the script on GO statements
        var batches = script
            .Split(new[] { "GO" }, StringSplitOptions.RemoveEmptyEntries)
            .Where(batch => !string.IsNullOrWhiteSpace(batch));

        using (var connection = new SqlConnection(_sqlContainer.GetConnectionString()))
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

    public async Task DisposeAsync()
    {
        await _sqlContainer.StopAsync();
        await _sqlContainer.DisposeAsync();
    }
}
