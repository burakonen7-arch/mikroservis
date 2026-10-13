namespace Shared.Application.Abstractions;

public interface IOutbox
{
    void Add<T>(T applicationEvent) where T : IApplicationEvent;
}

// IOutbox: Uygulama katmanının RabbitMQ veya veritabanı ayrıntılarına bağımlı olmadan yayımlanacak olayları outbox akışına eklemesini sağlar.
