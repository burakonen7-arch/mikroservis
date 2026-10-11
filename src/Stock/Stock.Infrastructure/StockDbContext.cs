using Microsoft.EntityFrameworkCore;
using Stock.Domain.Entities;
using BuildingBlocks.Persistence;
namespace Stock.Infrastructure;

public sealed class StockDbContext(DbContextOptions<StockDbContext> options) : DbContext(options)
{
    public DbSet<StockItem> Stocks => Set<StockItem>();
    public DbSet<OutboxMessage> Outbox => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.ApplyConfigurationsFromAssembly(typeof(StockDbContext).Assembly);
    }
}
