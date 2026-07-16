using ANU_Admissions.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace ANU_Admissions.SqlServerTests;

internal sealed class SqlServerTestDatabase : IAsyncDisposable
{
    public const string ConnectionStringEnvironmentVariable = "ANU_TEST_SQLSERVER";

    private readonly string _databaseName;
    private readonly string _masterConnectionString;

    private SqlServerTestDatabase(
        string databaseName,
        string masterConnectionString,
        string connectionString)
    {
        _databaseName = databaseName;
        _masterConnectionString = masterConnectionString;
        ConnectionString = connectionString;
    }

    public string ConnectionString { get; }

    public static async Task<SqlServerTestDatabase> CreateAsync(
        CancellationToken cancellationToken)
    {
        var configured = Environment.GetEnvironmentVariable(
            ConnectionStringEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(configured))
        {
            throw new InvalidOperationException(
                $"Set {ConnectionStringEnvironmentVariable} to an isolated SQL Server " +
                "instance before running provider integration tests.");
        }

        var databaseName = $"ANU_Admissions_Test_{Guid.NewGuid():N}";
        var masterBuilder = new SqlConnectionStringBuilder(configured)
        {
            InitialCatalog = "master",
            Pooling = false,
            ConnectRetryCount = 0
        };
        var databaseBuilder = new SqlConnectionStringBuilder(masterBuilder.ConnectionString)
        {
            InitialCatalog = databaseName
        };

        var database = new SqlServerTestDatabase(
            databaseName,
            masterBuilder.ConnectionString,
            databaseBuilder.ConnectionString);

        await database.CreateDatabaseAsync(cancellationToken);
        try
        {
            await using var context = database.CreateContext();
            await context.Database.MigrateAsync(cancellationToken);
            return database;
        }
        catch
        {
            await database.DisposeAsync();
            throw;
        }
    }

    public AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(ConnectionString)
            .EnableDetailedErrors()
            .Options;
        return new AppDbContext(options);
    }

    private async Task CreateDatabaseAsync(CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(_masterConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $"CREATE DATABASE [{_databaseName}]";
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        SqlConnection.ClearAllPools();
        await using var connection = new SqlConnection(_masterConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            IF DB_ID(N'{_databaseName}') IS NOT NULL
            BEGIN
                ALTER DATABASE [{_databaseName}]
                    SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                DROP DATABASE [{_databaseName}];
            END
            """;
        await command.ExecuteNonQueryAsync();
    }
}
