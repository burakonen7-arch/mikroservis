using Product.Domain.Entities;

namespace Product.Application.DTOs;

public sealed record ProductDto(Guid Id, string Name, decimal Price, int AvailableQuantity, long StockVersion)
{
    public static ProductDto FromEntity(ProductItem product) =>
        new(product.Id, product.Name, product.Price, product.AvailableQuantity, product.StockVersion);
}
