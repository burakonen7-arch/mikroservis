using Microsoft.EntityFrameworkCore;
using Product.Domain.Entities;
using BuildingBlocks.Persistence;
namespace Product.Infrastructure;

public sealed class ProductDbContext(DbContextOptions<ProductDbContext> options) : DbContext(options)
{
    public DbSet<ProductItem> Products => Set<ProductItem>();
    public DbSet<OutboxMessage> Outbox => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.ApplyConfigurationsFromAssembly(typeof(ProductDbContext).Assembly);
    }
}
