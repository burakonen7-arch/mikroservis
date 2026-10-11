using Microsoft.EntityFrameworkCore;
using Stock.Domain;
using Contracts;
namespace Stock.Application;

public abstract class StockDataContext(DbContextOptions options) : DbContext(options)
{
    public DbSet<StockItem> Stocks => Set<StockItem>();
    public DbSet<OutboxMessage> Outbox => Set<OutboxMessage>();
}
