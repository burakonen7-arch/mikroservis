using Stock.Domain.Entities;

namespace Stock.Application.Abstractions;

public interface IStockWriteRepository
{
    Task<StockItem?> GetByProductIdForUpdateAsync(Guid productId, CancellationToken cancellationToken);
    Task<bool> ExistsAsync(Guid productId, CancellationToken cancellationToken);
    void Add(StockItem stock);
}
