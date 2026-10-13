using Microsoft.EntityFrameworkCore;
using Product.Application.Abstractions;
using Product.Application.DTOs;
using BuildingBlocks.Observability;

namespace Product.Infrastructure.Repositories;

public sealed class ProductReadRepository(ProductDbContext db) : IProductReadRepository
{
    public async Task<ProductDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        using var activity = OperationTelemetry.Database("mongodb.products.read", "mongodb", id.ToString());
        var product = await db.Products.AsNoTracking()
            .SingleOrDefaultAsync(product => product.Id == id, cancellationToken);

        return product is null ? null : ProductDto.FromEntity(product);
    }
}
