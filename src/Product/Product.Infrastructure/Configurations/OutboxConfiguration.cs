using BuildingBlocks.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MongoDB.EntityFrameworkCore.Extensions;

namespace Product.Infrastructure.Configurations;

public sealed class OutboxConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToCollection("outbox");
        builder.HasKey(message => message.Id);
    }
}

// OutboxConfiguration: Ürün servisindeki outbox mesajlarını MongoDB içindeki outbox koleksiyonuna eşler ve Id alanını anahtar olarak tanımlar.
