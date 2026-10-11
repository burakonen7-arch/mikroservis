using Stock.Domain.Entities;

namespace Stock.Application.Abstractions;

public interface IStockRepository
{
    Task<StockItem?> GetByProductIdAsync(Guid productId, CancellationToken cancellationToken);
    Task<StockItem?> GetByProductIdForUpdateAsync(Guid productId, CancellationToken cancellationToken);
    Task<bool> ExistsAsync(Guid productId, CancellationToken cancellationToken);
    void Add(StockItem stock);
}
