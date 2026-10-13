using Shared.Application.Abstractions;
using MediatR;
using Shared.Application.Exceptions;
using Stock.Application.Abstractions;
using Stock.Application.Caching;
using Stock.Application.DTOs;
using Stock.Application.Events;

namespace Stock.Application.Commands.AdjustStock;

public sealed class AdjustStockCommandHandler(
    IStockWriteRepository stocks,
    IUnitOfWork unitOfWork,
    IOutbox outbox,
    ICache cache) : IRequestHandler<AdjustStockCommand, StockDto?>
{
    public async Task<StockDto?> Handle(AdjustStockCommand command, CancellationToken cancellationToken)
    {
        var stock = await stocks.GetByProductIdForUpdateAsync(command.ProductId, cancellationToken);
        if (stock is null)
            return null;
        if (stock.Version != command.ExpectedVersion)
            throw new ConcurrentUpdateException("Stock version has changed; reload and retry.");

        stock.Adjust(command.Delta);
        outbox.Add(new StockChangedEvent(stock.ProductId, stock.Quantity, stock.Version));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await cache.Remove(StockCacheKeys.ByProductId(command.ProductId), cancellationToken);

        return StockDto.FromEntity(stock);
    }
}

// AdjustStockCommandHandler: Beklenen sürümü doğrulayarak stok miktarını değiştirir ve StockChangedEvent olayını outbox akışına ekler.
// Stok ile olay kaydını birlikte kaydeder, ardından stok önbelleğini temizler.
