using MediatR;
using Product.Application.DTOs;

namespace Product.Application.Commands.UpdateProduct;

public sealed record UpdateProductCommand(Guid Id, string Name, decimal Price) : IRequest<ProductDto?>;
