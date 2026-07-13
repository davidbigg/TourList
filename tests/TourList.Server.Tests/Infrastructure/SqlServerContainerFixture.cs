using Microsoft.Data.SqlClient;
using Testcontainers.MsSql;

namespace TourList.Server.Tests.Infrastructure;

/// <summary>
/// Starts a single real SQL Server container for the test run (matching the Aspire-provisioned dev DB).
/// Each test class carves out its own database via <see cref="ConnectionStringFor"/> so state never leaks
/// between classes. EF's <c>MigrateAsync</c> creates the database on first use.
/// </summary>
public sealed class SqlServerContainerFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _container =
        new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    public string ConnectionStringFor(string databaseName)
    {
        var builder = new SqlConnectionStringBuilder(_container.GetConnectionString())
        {
            InitialCatalog = databaseName,
            TrustServerCertificate = true,
        };
        return builder.ConnectionString;
    }

    public async Task InitializeAsync() => await _container.StartAsync();

    public async Task DisposeAsync() => await _container.DisposeAsync();
}

[CollectionDefinition(Name)]
public sealed class SqlServerCollection : ICollectionFixture<SqlServerContainerFixture>
{
    public const string Name = "sqlserver";
}
