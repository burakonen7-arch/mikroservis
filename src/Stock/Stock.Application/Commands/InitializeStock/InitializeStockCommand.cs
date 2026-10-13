using MediatR;

namespace Stock.Application.Commands.InitializeStock;

public sealed record InitializeStockCommand(Guid ProductId) : IRequest;
