using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TourList.Server.Data;

/// <summary>
/// Design-time factory for <c>dotnet ef</c>. At runtime the connection string is injected by
/// Aspire (<c>AddSqlServerDbContext("tourlist")</c>); migration tooling never connects, so any
/// well-formed SQL Server connection string suffices to build the model.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer("Server=localhost;Database=tourlist;Trusted_Connection=True;TrustServerCertificate=True;")
            .Options;

        return new AppDbContext(options);
    }
}
