using Shared.Application.Abstractions;

namespace BuildingBlocks.Persistence;

public interface IOutboxMessageFactory
{
    OutboxMessage Create(IApplicationEvent applicationEvent);
}
