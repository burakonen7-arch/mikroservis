using Microsoft.EntityFrameworkCore;
using Stock.Application.Abstractions;
using Stock.Domain.Entities;
using BuildingBlocks.Observability;

namespace Stock.Infrastructure.Repositories;

public sealed class StockWriteRepository(StockDbContext db) : IStockWriteRepository
{
    public async Task<StockItem?> GetByProductIdForUpdateAsync(Guid productId, CancellationToken cancellationToken)
    {
        using var activity = OperationTelemetry.Database("oracle.stocks.read_for_update", "oracle", productId.ToString());
        return await db.Stocks.SingleOrDefaultAsync(stock => stock.ProductId == productId, cancellationToken);
    }

    public async Task<bool> ExistsAsync(Guid productId, CancellationToken cancellationToken)
    {
        using var activity = OperationTelemetry.Database("oracle.stocks.exists", "oracle", productId.ToString());
        return await db.Stocks.AnyAsync(stock => stock.ProductId == productId, cancellationToken);
    }

    public void Add(StockItem stock) => db.Stocks.Add(stock);
}
