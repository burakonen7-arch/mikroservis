using Microsoft.EntityFrameworkCore;
using Stock.Application.Abstractions;
using Stock.Application.DTOs;
using BuildingBlocks.Observability;

namespace Stock.Infrastructure.Repositories;

public sealed class StockReadRepository(StockDbContext db) : IStockReadRepository
{
    public async Task<StockDto?> GetByProductIdAsync(Guid productId, CancellationToken cancellationToken)
    {
        using var activity = OperationTelemetry.Database("oracle.stocks.read", "oracle", productId.ToString());
        var stock = await db.Stocks.AsNoTracking()
            .SingleOrDefaultAsync(stock => stock.ProductId == productId, cancellationToken);

        return stock is null ? null : StockDto.FromEntity(stock);
    }
}
