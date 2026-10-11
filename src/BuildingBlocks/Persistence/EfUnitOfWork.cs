using Shared.Application.Abstractions;
using Shared.Application.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace BuildingBlocks.Persistence;

public sealed class EfUnitOfWork<TContext>(TContext db, IOutboxMessageFactory messages) : IUnitOfWork, IOutbox
    where TContext : DbContext
{
    public void Add<T>(T applicationEvent) where T : IApplicationEvent =>
        db.Set<OutboxMessage>().Add(messages.Create(applicationEvent));

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            // The entity changes and outbox entries share this context and one transaction.
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new ConcurrentUpdateException("The resource has changed; reload and retry.", exception);
        }
    }
}
