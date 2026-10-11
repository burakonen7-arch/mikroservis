using Shared.Application.Abstractions;

namespace Product.Application.Events;

public sealed record ProductCreatedEvent(Guid ProductId, string Name) : IApplicationEvent;
