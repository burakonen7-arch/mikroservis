using Microsoft.EntityFrameworkCore;
using Product.Domain;
using Contracts;
namespace Product.Application;

public abstract class ProductDataContext(DbContextOptions options) : DbContext(options)
{
    public DbSet<ProductItem> Products => Set<ProductItem>();
    public DbSet<OutboxMessage> Outbox => Set<OutboxMessage>();
}
