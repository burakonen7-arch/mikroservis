using Microsoft.EntityFrameworkCore;
using Stock.Application.Abstractions;
using Stock.Domain.Entities;

namespace Stock.Infrastructure.Repositories;

public sealed class StockRepository(StockDbContext db) : IStockRepository
{
    public Task<StockItem?> GetByProductIdAsync(Guid productId, CancellationToken cancellationToken) =>
        db.Stocks.AsNoTracking().SingleOrDefaultAsync(stock => stock.ProductId == productId, cancellationToken);

    public Task<StockItem?> GetByProductIdForUpdateAsync(Guid productId, CancellationToken cancellationToken) =>
        db.Stocks.SingleOrDefaultAsync(stock => stock.ProductId == productId, cancellationToken);

    public Task<bool> ExistsAsync(Guid productId, CancellationToken cancellationToken) =>
        db.Stocks.AnyAsync(stock => stock.ProductId == productId, cancellationToken);

    public void Add(StockItem stock) => db.Stocks.Add(stock);
}
