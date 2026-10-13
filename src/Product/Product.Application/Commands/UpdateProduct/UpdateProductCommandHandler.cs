using MediatR;
using Product.Application.Abstractions;
using Product.Application.Caching;
using Product.Application.DTOs;
using Shared.Application.Abstractions;

namespace Product.Application.Commands.UpdateProduct;

public sealed class UpdateProductCommandHandler(
    IProductWriteRepository products,
    IUnitOfWork unitOfWork,
    ICache cache) : IRequestHandler<UpdateProductCommand, ProductDto?>
{
    public async Task<ProductDto?> Handle(UpdateProductCommand command, CancellationToken cancellationToken)
    {
        var product = await products.GetByIdForUpdateAsync(command.Id, cancellationToken);
        if (product is null)
            return null;

        product.UpdateDetails(command.Name, command.Price);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await cache.Remove(ProductCacheKeys.ById(command.Id), cancellationToken);

        return ProductDto.FromEntity(product);
    }
}
