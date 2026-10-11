namespace Contracts.Events;

public sealed record ProductCreated(Guid EventId, Guid ProductId, string Name);
