using Product.Domain.Entities;

namespace Product.Application.Abstractions;

public interface IProductWriteRepository
{
    Task<ProductItem?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken);
    void Add(ProductItem product);
    void Remove(ProductItem product);
}
