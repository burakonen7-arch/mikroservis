using Stock.Domain.Entities;

namespace Stock.Application.DTOs;

public sealed record StockDto(Guid ProductId, int Quantity, long Version)
{
    public static StockDto FromEntity(StockItem stock) => new(stock.ProductId, stock.Quantity, stock.Version);
}
