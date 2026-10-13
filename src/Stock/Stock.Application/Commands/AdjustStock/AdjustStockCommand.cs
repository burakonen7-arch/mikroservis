using MediatR;
using Stock.Application.DTOs;

namespace Stock.Application.Commands.AdjustStock;

public sealed record AdjustStockCommand(Guid ProductId, int Delta, long ExpectedVersion) : IRequest<StockDto?>;
