using Product.Application.DTOs;

namespace Product.Application.Abstractions;

public interface IProductReadRepository
{
    Task<ProductDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
}
