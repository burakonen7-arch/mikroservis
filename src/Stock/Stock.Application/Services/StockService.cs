using Shared.Application.Abstractions;
using Shared.Application.Exceptions;
using Stock.Application.Abstractions;
using Stock.Application.DTOs;
using Stock.Application.Events;
using Stock.Domain.Entities;

namespace Stock.Application.Services;

public sealed class StockService(
    IStockRepository stocks,
    IUnitOfWork unitOfWork,
    IOutbox outbox,
    ICache cache) : IStockService
{
    public async Task<StockDto?> GetByProductIdAsync(Guid productId, CancellationToken cancellationToken)
    {
        var key = CacheKey(productId);
        var cached = await cache.Get<StockDto>(key, cancellationToken);
        if (cached is not null)
            return cached;

        var stock = await stocks.GetByProductIdAsync(productId, cancellationToken);
        if (stock is null)
            return null;

        var result = StockDto.FromEntity(stock);
        await cache.Put(key, result, cancellationToken);
        return result;
    }

    public async Task<StockDto?> AdjustAsync(Guid productId, AdjustStockInput input, CancellationToken cancellationToken)
    {
        var stock = await stocks.GetByProductIdForUpdateAsync(productId, cancellationToken);
        if (stock is null)
            return null;
        if (stock.Version != input.ExpectedVersion)
            throw new ConcurrentUpdateException("Stock version has changed; reload and retry.");

        stock.Adjust(input.Delta);
        AddStockChangedEvent(stock);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await cache.Remove(CacheKey(productId), cancellationToken);

        return StockDto.FromEntity(stock);
    }

    public async Task InitializeAsync(Guid productId, CancellationToken cancellationToken)
    {
        if (!await stocks.ExistsAsync(productId, cancellationToken))
        {
            var stock = new StockItem(productId);
            stocks.Add(stock);
            AddStockChangedEvent(stock);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        await cache.Remove(CacheKey(productId), cancellationToken);
    }

    private void AddStockChangedEvent(StockItem stock) =>
        outbox.Add(new StockChangedEvent(stock.ProductId, stock.Quantity, stock.Version));

    private static string CacheKey(Guid productId) => $"stock:{productId}";
}
