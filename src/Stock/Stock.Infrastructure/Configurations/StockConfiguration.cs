using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Stock.Domain.Entities;

namespace Stock.Infrastructure.Configurations;

public sealed class StockConfiguration : IEntityTypeConfiguration<StockItem>
{
    public void Configure(EntityTypeBuilder<StockItem> builder)
    {
        builder.ToTable("STOCKS");
        builder.HasKey(stock => stock.ProductId);
        builder.Property(stock => stock.Version).IsConcurrencyToken();
    }
}
