using Product.Application.DTOs;

namespace Product.Application.Abstractions;

public interface IProductService
{
    Task<ProductDto> CreateAsync(CreateProductInput input, CancellationToken cancellationToken);
    Task<ProductDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task ApplyStockAsync(Guid productId, int quantity, long version, CancellationToken cancellationToken);
}
