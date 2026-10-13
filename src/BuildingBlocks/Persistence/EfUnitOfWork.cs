using Shared.Application.Abstractions;
using Shared.Application.Exceptions;
using Microsoft.EntityFrameworkCore;
using BuildingBlocks.Observability;
using System.Diagnostics;

namespace BuildingBlocks.Persistence;

public sealed class EfUnitOfWork<TContext>(TContext db, IOutboxMessageFactory messages) : IUnitOfWork, IOutbox
    where TContext : DbContext
{
    public void Add<T>(T applicationEvent) where T : IApplicationEvent =>
        db.Set<OutboxMessage>().Add(messages.Create(applicationEvent));

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        using var activity = OperationTelemetry.Database("database.SaveChanges", db.Database.ProviderName ?? "database");
        try
        {
            // The entity changes and outbox entries share this context and one transaction.
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            activity?.SetStatus(ActivityStatusCode.Error, "Concurrent update");
            throw new ConcurrentUpdateException("The resource has changed; reload and retry.", exception);
        }
        catch (Exception exception)
        {
            activity?.SetStatus(ActivityStatusCode.Error, exception.GetType().Name);
            throw;
        }
    }
}

// EfUnitOfWork: İş verisi değişikliklerini ve outbox mesajlarını aynı DbContext üzerinden birlikte kaydeder.
// Uygulama olaylarını mesaj fabrikasıyla outbox kaydına çevirir; eşzamanlı güncelleme hatalarını uygulama katmanına uygun hataya dönüştürür.
