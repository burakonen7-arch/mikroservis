using Product.Application.DTOs;
using MediatR;

namespace Product.Application.Queries.GetProductById;

public sealed record GetProductByIdQuery(Guid Id) : IRequest<ProductDto?>;
