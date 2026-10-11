using Shared.Application.Abstractions;
using Product.Application.Abstractions;
using Product.Application.DTOs;
using Product.Application.Events;
using Product.Domain.Entities;

namespace Product.Application.Services;

public sealed class ProductService(
    IProductRepository products,
    IUnitOfWork unitOfWork,
    IOutbox outbox,
    ICache cache) : IProductService
{
    public async Task<ProductDto> CreateAsync(CreateProductInput input, CancellationToken cancellationToken)
    {
        var product = new ProductItem(Guid.NewGuid(), input.Name, input.Price);
        products.Add(product);
        outbox.Add(new ProductCreatedEvent(product.Id, product.Name));
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return ProductDto.FromEntity(product);
    }

    public async Task<ProductDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var key = CacheKey(id);
        var cached = await cache.Get<ProductDto>(key, cancellationToken);
        if (cached is not null)
            return cached;

        var product = await products.GetByIdAsync(id, cancellationToken);
        if (product is null)
            return null;

        var result = ProductDto.FromEntity(product);
        await cache.Put(key, result, cancellationToken);
        return result;
    }

    public async Task ApplyStockAsync(Guid productId, int quantity, long version, CancellationToken cancellationToken)
    {
        var product = await products.GetByIdForUpdateAsync(productId, cancellationToken)
            ?? throw new InvalidOperationException("Product not found.");

        if (product.ApplyStock(quantity, version))
            await unitOfWork.SaveChangesAsync(cancellationToken);

        // Redelivery must invalidate the cache even if the database write already succeeded.
        await cache.Remove(CacheKey(productId), cancellationToken);
    }

    private static string CacheKey(Guid id) => $"product:{id}";
}
