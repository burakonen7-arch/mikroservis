using Product.Domain.Entities;

namespace Product.Application.Abstractions;

public interface IProductRepository
{
    Task<ProductItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<ProductItem?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken);
    void Add(ProductItem product);
}
