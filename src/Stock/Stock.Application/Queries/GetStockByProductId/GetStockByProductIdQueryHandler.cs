using Shared.Application.Abstractions;
using MediatR;
using Stock.Application.Abstractions;
using Stock.Application.Caching;
using Stock.Application.DTOs;

namespace Stock.Application.Queries.GetStockByProductId;

public sealed class GetStockByProductIdQueryHandler(
    IStockReadRepository stocks,
    ICache cache) : IRequestHandler<GetStockByProductIdQuery, StockDto?>
{
    public async Task<StockDto?> Handle(GetStockByProductIdQuery query, CancellationToken cancellationToken)
    {
        var key = StockCacheKeys.ByProductId(query.ProductId);
        var cached = await cache.Get<StockDto>(key, cancellationToken);
        if (cached is not null)
            return cached;

        var stock = await stocks.GetByProductIdAsync(query.ProductId, cancellationToken);
        if (stock is null)
            return null;

        await cache.Put(key, stock, cancellationToken);
        return stock;
    }
}
