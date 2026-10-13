using Stock.Application.DTOs;

namespace Stock.Application.Abstractions;

public interface IStockReadRepository
{
    Task<StockDto?> GetByProductIdAsync(Guid productId, CancellationToken cancellationToken);
}
