using Microsoft.EntityFrameworkCore;
using MongoDB.EntityFrameworkCore.Extensions;
using Product.Application;
using Product.Domain;
using Contracts;
namespace Product.Infrastructure;

public sealed class ProductDbContext(DbContextOptions<ProductDbContext> options) : ProductDataContext(options)
{
    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<ProductItem>().ToCollection("products"); model.Entity<ProductItem>().HasKey(x => x.Id);
        model.Entity<ProductItem>().Property(x => x.StockVersion).IsConcurrencyToken();
        model.Entity<OutboxMessage>().ToCollection("outbox"); model.Entity<OutboxMessage>().HasKey(x => x.Id);
    }
}
