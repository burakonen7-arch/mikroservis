using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MongoDB.EntityFrameworkCore.Extensions;
using Product.Infrastructure.Idempotency;

namespace Product.Infrastructure.Configurations;

public sealed class ProductCreationRequestConfiguration : IEntityTypeConfiguration<ProductCreationRequest>
{
    public void Configure(EntityTypeBuilder<ProductCreationRequest> builder)
    {
        builder.ToCollection("product_creation_requests");
        builder.HasKey(request => request.Id);
    }
}
