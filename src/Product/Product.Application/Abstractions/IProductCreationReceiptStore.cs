using Product.Application.DTOs;

namespace Product.Application.Abstractions;

public interface IProductCreationReceiptStore
{
    Task<ProductCreationReceipt?> FindAsync(string id, CancellationToken cancellationToken);
    void Add(ProductCreationReceipt receipt);
}

public sealed record ProductCreationReceipt(string Id, string Fingerprint, ProductDto Result, DateTime CreatedAt);
