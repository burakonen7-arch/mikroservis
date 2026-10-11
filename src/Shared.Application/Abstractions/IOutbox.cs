namespace Shared.Application.Abstractions;

public interface IOutbox
{
    void Add<T>(T applicationEvent) where T : IApplicationEvent;
}
