using Shared.Application.Abstractions;

namespace BuildingBlocks.Persistence;

public interface IOutboxMessageFactory
{
    OutboxMessage Create(IApplicationEvent applicationEvent);
}

// IOutboxMessageFactory: Uygulama olayını uygun entegrasyon mesajına ve topic bilgisine sahip bir outbox kaydına dönüştürme sözleşmesidir.
