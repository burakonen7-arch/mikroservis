using Microsoft.EntityFrameworkCore;
using Stock.Application;
using Stock.Domain;
using Contracts;
namespace Stock.Infrastructure;

public sealed class StockDbContext(DbContextOptions<StockDbContext> options) : StockDataContext(options)
{
    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<StockItem>().ToTable("STOCKS"); model.Entity<StockItem>().HasKey(x => x.ProductId);
        model.Entity<StockItem>().Property(x => x.Version).IsConcurrencyToken();
        model.Entity<OutboxMessage>().ToTable("OUTBOX"); model.Entity<OutboxMessage>().HasKey(x => x.Id);
        model.Entity<OutboxMessage>().Property(x => x.Topic).HasMaxLength(200);
        model.Entity<OutboxMessage>().Property(x => x.TraceParent).HasMaxLength(100);
        model.Entity<OutboxMessage>().Property(x => x.Payload).HasColumnType("CLOB");
    }
}
