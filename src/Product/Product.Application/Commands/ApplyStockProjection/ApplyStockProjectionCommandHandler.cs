using Product.Application.Abstractions;
using Product.Application.Caching;
using Shared.Application.Abstractions;
using MediatR;

namespace Product.Application.Commands.ApplyStockProjection;

public sealed class ApplyStockProjectionCommandHandler(
    IProductWriteRepository products,
    IUnitOfWork unitOfWork,
    ICache cache) : IRequestHandler<ApplyStockProjectionCommand>
{
    public async Task Handle(ApplyStockProjectionCommand command, CancellationToken cancellationToken)
    {
        var product = await products.GetByIdForUpdateAsync(command.ProductId, cancellationToken);

        // Stock messages already in flight may arrive after the product was removed.
        if (product is not null && product.ApplyStock(command.Quantity, command.Version))
            await unitOfWork.SaveChangesAsync(cancellationToken);

        // Redelivery must invalidate the cache even if the database write already succeeded.
        await cache.Remove(ProductCacheKeys.ById(command.ProductId), cancellationToken);
    }
}

// ApplyStockProjectionCommandHandler: RabbitMQ üzerinden gelen stok bilgisini ürünün stok görünümüne uygular.
// Güncelleme kararını ürünün sürüm kontrolüne bırakır; tekrar teslimlerde de ürün önbelleğini temizler.
