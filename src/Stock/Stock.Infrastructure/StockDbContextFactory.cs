using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
namespace Stock.Infrastructure;

// EF tooling only: schema generation is offline, deployment supplies the actual connection.
public sealed class StockDbContextFactory : IDesignTimeDbContextFactory<StockDbContext>
{
    public StockDbContext CreateDbContext(string[] args) => new(
        new DbContextOptionsBuilder<StockDbContext>()
            .UseOracle(Environment.GetEnvironmentVariable("ConnectionStrings__Database")
                ?? "User Id=stock;Data Source=localhost:1521/FREEPDB1")
            .Options);
}
