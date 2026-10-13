using Product.Application.DTOs;

namespace Product.Application.Commands.CreateProduct;

public sealed record CreateProductResult(ProductDto Product, bool Replayed);
