using MediatR;
using Stock.Application.DTOs;

namespace Stock.Application.Queries.GetStockByProductId;

public sealed record GetStockByProductIdQuery(Guid ProductId) : IRequest<StockDto?>;
