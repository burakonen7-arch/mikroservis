using MediatR;

namespace Product.Application.Commands.RemoveProduct;

public sealed record RemoveProductCommand(Guid Id) : IRequest<bool>;
