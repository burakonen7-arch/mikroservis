using Microsoft.EntityFrameworkCore;
using Contracts;
using Stock.Domain;
using System.Text.Json;
namespace Stock.Application;

public sealed record AdjustStockCommand(Guid ProductId, int Delta, long ExpectedVersion);
public sealed record GetStockQuery(Guid ProductId);
public sealed record StockView(Guid ProductId, int Quantity, long Version);
public sealed class AdjustStockHandler(StockDataContext db, ICache cache)
{
    public async Task<StockView?> Handle(AdjustStockCommand command, CancellationToken ct)
    {
        var stock = await db.Stocks.SingleOrDefaultAsync(p => p.ProductId == command.ProductId, ct); if (stock is null) return null;
        if (stock.Version != command.ExpectedVersion) throw new DbUpdateConcurrencyException("Stock version has changed; reload and retry.");
        stock.Adjust(command.Delta);
        db.Outbox.Add(OutboxMessage.Create(Topics.StockChanged, new StockChanged(Guid.NewGuid(), stock.ProductId, stock.Quantity, stock.Version)));
        await db.SaveChangesAsync(ct); await cache.Remove($"stock:{stock.ProductId}", ct); return new(stock.ProductId, stock.Quantity, stock.Version);
    }
}
public sealed class GetStockHandler(StockDataContext db, ICache cache)
{
    public async Task<StockView?> Handle(GetStockQuery query, CancellationToken ct)
    {
        var key = $"stock:{query.ProductId}"; var cached = await cache.Get<StockView>(key, ct); if (cached is not null) return cached;
        var p = await db.Stocks.AsNoTracking().SingleOrDefaultAsync(p => p.ProductId == query.ProductId, ct); if (p is null) return null;
        var view = new StockView(p.ProductId, p.Quantity, p.Version); await cache.Put(key, view, ct); return view;
    }
}
public sealed class ProductCreatedHandler(StockDataContext db, ICache cache) : IMessageHandler
{
    public async Task Handle(string topic, string payload, CancellationToken ct)
    {
        if (topic != Topics.ProductCreated) throw new ArgumentException("Unexpected topic");
        var message = JsonSerializer.Deserialize<ProductCreated>(payload) ?? throw new ArgumentException("Invalid message");
        if (!await db.Stocks.AnyAsync(x => x.ProductId == message.ProductId, ct))
        {
            var stock = new StockItem(message.ProductId); db.Stocks.Add(stock);
            db.Outbox.Add(OutboxMessage.Create(Topics.StockChanged, new StockChanged(Guid.NewGuid(), stock.ProductId, stock.Quantity, stock.Version)));
            await db.SaveChangesAsync(ct);
        }
        await cache.Remove($"stock:{message.ProductId}", ct);
    }
}
