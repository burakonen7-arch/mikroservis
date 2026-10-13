using Shared.Application.Abstractions;
using MediatR;
using Stock.Application.Abstractions;
using Stock.Application.Caching;
using Stock.Application.Events;
using Stock.Domain.Entities;

namespace Stock.Application.Commands.InitializeStock;

public sealed class InitializeStockCommandHandler(
    IStockWriteRepository stocks,
    IUnitOfWork unitOfWork,
    IOutbox outbox,
    ICache cache) : IRequestHandler<InitializeStockCommand>
{
    public async Task Handle(InitializeStockCommand command, CancellationToken cancellationToken)
    {
        if (!await stocks.ExistsAsync(command.ProductId, cancellationToken))
        {
            var stock = new StockItem(command.ProductId);
            stocks.Add(stock);
            outbox.Add(new StockChangedEvent(stock.ProductId, stock.Quantity, stock.Version));
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        await cache.Remove(StockCacheKeys.ByProductId(command.ProductId), cancellationToken);
    }
}

// InitializeStockCommandHandler: Ürün oluşturulma mesajının tetiklediği komutla, yoksa başlangıç stok kaydını oluşturur.
// Yeni stok için StockChangedEvent olayını outbox akışına ekleyip birlikte kaydeder ve stok önbelleğini temizler.
