using Product.Application.Abstractions;
using Product.Application.DTOs;
using Product.Application.Events;
using Product.Domain.Entities;
using Shared.Application.Abstractions;
using MediatR;

namespace Product.Application.Commands.CreateProduct;

public sealed class CreateProductCommandHandler(
    IProductWriteRepository products,
    IUnitOfWork unitOfWork,
    IOutbox outbox) : IRequestHandler<CreateProductCommand, ProductDto>
{
    public async Task<ProductDto> Handle(CreateProductCommand command, CancellationToken cancellationToken)
    {
        var product = new ProductItem(Guid.NewGuid(), command.Name, command.Price);
        products.Add(product);
        outbox.Add(new ProductCreatedEvent(product.Id, product.Name));
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return ProductDto.FromEntity(product);
    }
}

// CreateProductCommandHandler: Yeni ürünü oluşturur ve ProductCreatedEvent olayını outbox akışına ekler.
// Ürün ile olay kaydını aynı unit of work üzerinden kaydeder; olay daha sonra RabbitMQ üzerinden yayımlanır.
