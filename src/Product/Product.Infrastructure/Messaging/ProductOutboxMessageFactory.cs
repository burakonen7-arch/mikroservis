using BuildingBlocks.Persistence;
using Contracts.Events;
using Contracts.Messaging;
using Product.Application.Events;
using Shared.Application.Abstractions;

namespace Product.Infrastructure.Messaging;

public sealed class ProductOutboxMessageFactory : IOutboxMessageFactory
{
    public OutboxMessage Create(IApplicationEvent applicationEvent) => applicationEvent switch
    {
        ProductCreatedEvent message => OutboxMessage.Create(Topics.ProductCreated,
            new ProductCreated(Guid.NewGuid(), message.ProductId, message.Name)),
        _ => throw new InvalidOperationException($"Unsupported product event: {applicationEvent.GetType().Name}.")
    };
}

// ProductOutboxMessageFactory: ProductCreatedEvent olayını servisler arası ProductCreated mesajına dönüştürür.
// Mesajı product.created topic bilgisiyle outbox kaydı olarak hazırlar; desteklenmeyen olayları reddeder.
