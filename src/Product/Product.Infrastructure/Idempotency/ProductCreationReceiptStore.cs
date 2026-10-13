using Microsoft.EntityFrameworkCore;
using Product.Application.Abstractions;
using Product.Application.DTOs;
using BuildingBlocks.Observability;

namespace Product.Infrastructure.Idempotency;

public sealed class ProductCreationReceiptStore(ProductDbContext db) : IProductCreationReceiptStore
{
    public async Task<ProductCreationReceipt?> FindAsync(string id, CancellationToken cancellationToken)
    {
        using var activity = OperationTelemetry.Database("mongodb.idempotency.read", "mongodb");
        var receipt = await db.Set<ProductCreationRequest>().AsNoTracking()
            .SingleOrDefaultAsync(request => request.Id == id, cancellationToken);
        return receipt is null ? null : new ProductCreationReceipt(receipt.Id, receipt.Fingerprint,
            new ProductDto(receipt.ProductId, receipt.Name, receipt.Price, receipt.AvailableQuantity, receipt.StockVersion), receipt.CreatedAt);
    }

    public void Add(ProductCreationReceipt receipt) => db.Set<ProductCreationRequest>().Add(new ProductCreationRequest
    {
        Id = receipt.Id,
        Fingerprint = receipt.Fingerprint,
        ProductId = receipt.Result.Id,
        Name = receipt.Result.Name,
        Price = receipt.Result.Price,
        AvailableQuantity = receipt.Result.AvailableQuantity,
        StockVersion = receipt.Result.StockVersion,
        CreatedAt = receipt.CreatedAt
    });
}
