using BuildingBlocks.Persistence;
using MongoDB.Bson;
using MongoDB.Driver;
using Shared.Application.Abstractions;
using Shared.Application.Exceptions;

namespace Product.Infrastructure.Idempotency;

public sealed class ProductUnitOfWork(EfUnitOfWork<ProductDbContext> inner) : IUnitOfWork
{
    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await inner.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception) when (IsConcurrentWrite(exception))
        {
            throw new ConcurrentUpdateException("A concurrent request changed this resource; retry using the same Idempotency-Key.", exception);
        }
    }

    private static bool IsConcurrentWrite(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is MongoCommandException command && command.Code is 11000 or 112) return true;
            if (current is MongoWriteException write && write.WriteError.Code is 11000 or 112) return true;
            if (current is MongoBulkWriteException<BsonDocument> bulk && bulk.WriteErrors.Any(error => error.Code is 11000 or 112)) return true;
        }
        return false;
    }
}
