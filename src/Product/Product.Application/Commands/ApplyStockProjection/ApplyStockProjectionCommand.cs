using MediatR;

namespace Product.Application.Commands.ApplyStockProjection;

public sealed record ApplyStockProjectionCommand(Guid ProductId, int Quantity, long Version) : IRequest;
