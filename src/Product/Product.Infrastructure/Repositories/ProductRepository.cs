using Microsoft.EntityFrameworkCore;
using Product.Application.Abstractions;
using Product.Domain.Entities;

namespace Product.Infrastructure.Repositories;

public sealed class ProductRepository(ProductDbContext db) : IProductRepository
{
    public Task<ProductItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        db.Products.AsNoTracking().SingleOrDefaultAsync(product => product.Id == id, cancellationToken);

    public Task<ProductItem?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
        db.Products.SingleOrDefaultAsync(product => product.Id == id, cancellationToken);

    public void Add(ProductItem product) => db.Products.Add(product);
}
