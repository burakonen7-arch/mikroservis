using BuildingBlocks.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Stock.Infrastructure.Configurations;

public sealed class OutboxConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("OUTBOX");
        builder.HasKey(message => message.Id);
        builder.Property(message => message.Topic).HasMaxLength(200);
        builder.Property(message => message.TraceParent).HasMaxLength(100);
        builder.Property(message => message.Payload).HasColumnType("CLOB");
    }
}

// OutboxConfiguration: Stok servisindeki outbox mesajlarını Oracle içindeki OUTBOX tablosuna eşler.
// Anahtarı, topic ve izleme alanlarının uzunluklarını ve JSON içeriği için CLOB sütun tipini yapılandırır.
