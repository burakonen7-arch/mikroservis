using Product.Application.DTOs;
using MediatR;

namespace Product.Application.Commands.CreateProduct;

public sealed record CreateProductCommand(string Name, decimal Price) : IRequest<ProductDto>;
