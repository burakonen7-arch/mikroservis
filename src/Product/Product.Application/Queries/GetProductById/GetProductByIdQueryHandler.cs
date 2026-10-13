using Product.Application.Abstractions;
using Product.Application.Caching;
using Product.Application.DTOs;
using Shared.Application.Abstractions;
using MediatR;

namespace Product.Application.Queries.GetProductById;

public sealed class GetProductByIdQueryHandler(
    IProductReadRepository products,
    ICache cache) : IRequestHandler<GetProductByIdQuery, ProductDto?>
{
    public async Task<ProductDto?> Handle(GetProductByIdQuery query, CancellationToken cancellationToken)
    {
        var key = ProductCacheKeys.ById(query.Id);
        var cached = await cache.Get<ProductDto>(key, cancellationToken);
        if (cached is not null)
            return cached;

        var product = await products.GetByIdAsync(query.Id, cancellationToken);
        if (product is null)
            return null;

        await cache.Put(key, product, cancellationToken);
        return product;
    }
}
