using MediatR;
using Product.Application.Abstractions;
using Product.Application.Caching;
using Shared.Application.Abstractions;

namespace Product.Application.Commands.RemoveProduct;

public sealed class RemoveProductCommandHandler(
    IProductWriteRepository products,
    IUnitOfWork unitOfWork,
    ICache cache) : IRequestHandler<RemoveProductCommand, bool>
{
    public async Task<bool> Handle(RemoveProductCommand command, CancellationToken cancellationToken)
    {
        var product = await products.GetByIdForUpdateAsync(command.Id, cancellationToken);
        if (product is not null)
        {
            products.Remove(product);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // A retry must invalidate the cache even when the previous delete already committed.
        await cache.Remove(ProductCacheKeys.ById(command.Id), cancellationToken);
        return product is not null;
    }
}
