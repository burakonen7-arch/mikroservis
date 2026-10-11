using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MongoDB.EntityFrameworkCore.Extensions;
using Product.Domain.Entities;

namespace Product.Infrastructure.Configurations;

public sealed class ProductConfiguration : IEntityTypeConfiguration<ProductItem>
{
    public void Configure(EntityTypeBuilder<ProductItem> builder)
    {
        builder.ToCollection("products");
        builder.HasKey(product => product.Id);
        builder.Property(product => product.StockVersion).IsConcurrencyToken();
    }
}
