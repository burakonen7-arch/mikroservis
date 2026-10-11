using Stock.Application.DTOs;

namespace Stock.Application.Abstractions;

public interface IStockService
{
    Task<StockDto?> GetByProductIdAsync(Guid productId, CancellationToken cancellationToken);
    Task<StockDto?> AdjustAsync(Guid productId, AdjustStockInput input, CancellationToken cancellationToken);
    Task InitializeAsync(Guid productId, CancellationToken cancellationToken);
}
