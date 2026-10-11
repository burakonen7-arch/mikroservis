using Microsoft.EntityFrameworkCore;
using Contracts;
using Product.Domain;
using System.Text.Json;
namespace Product.Application;

public sealed record CreateProductCommand(string Name, decimal Price);
public sealed record GetProductQuery(Guid Id);
public sealed record ProductView(Guid Id, string Name, decimal Price, int AvailableQuantity, long StockVersion);
public sealed class CreateProductHandler(ProductDataContext db)
{
    public async Task<ProductView> Handle(CreateProductCommand command, CancellationToken ct)
    {
        var product = new ProductItem(Guid.NewGuid(), command.Name, command.Price); db.Products.Add(product);
        db.Outbox.Add(OutboxMessage.Create(Topics.ProductCreated, new ProductCreated(Guid.NewGuid(), product.Id, product.Name)));
        await db.SaveChangesAsync(ct); return new(product.Id, product.Name, product.Price, 0, 0);
    }
}
public sealed class GetProductHandler(ProductDataContext db, ICache cache)
{
    public async Task<ProductView?> Handle(GetProductQuery query, CancellationToken ct)
    {
        var key = $"product:{query.Id}"; var cached = await cache.Get<ProductView>(key, ct); if (cached is not null) return cached;
        var p = await db.Products.AsNoTracking().SingleOrDefaultAsync(p => p.Id == query.Id, ct); if (p is null) return null;
        var view = new ProductView(p.Id, p.Name, p.Price, p.AvailableQuantity, p.StockVersion); await cache.Put(key, view, ct); return view;
    }
}
public sealed class StockChangedHandler(ProductDataContext db, ICache cache) : IMessageHandler
{
    public async Task Handle(string topic, string payload, CancellationToken ct)
    {
        if (topic != Topics.StockChanged) throw new ArgumentException("Unexpected topic");
        var message = JsonSerializer.Deserialize<StockChanged>(payload) ?? throw new ArgumentException("Invalid message");
        var product = await db.Products.SingleOrDefaultAsync(p => p.Id == message.ProductId, ct) ?? throw new InvalidOperationException("Product not found");
        if (product.ApplyStock(message.Quantity, message.Version)) await db.SaveChangesAsync(ct);
        // Also invalidate on redelivery after a cache failure following a committed database write.
        await cache.Remove($"product:{message.ProductId}", ct);
    }
}
