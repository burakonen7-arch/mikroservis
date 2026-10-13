using Microsoft.EntityFrameworkCore;
using Product.Application.Abstractions;
using Product.Domain.Entities;
using BuildingBlocks.Observability;

namespace Product.Infrastructure.Repositories;

public sealed class ProductWriteRepository(ProductDbContext db) : IProductWriteRepository
{
    public async Task<ProductItem?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken)
    {
        using var activity = OperationTelemetry.Database("mongodb.products.read_for_update", "mongodb", id.ToString());
        return await db.Products.SingleOrDefaultAsync(product => product.Id == id, cancellationToken);
    }

    public void Add(ProductItem product) => db.Products.Add(product);

    public void Remove(ProductItem product) => db.Products.Remove(product);
}
