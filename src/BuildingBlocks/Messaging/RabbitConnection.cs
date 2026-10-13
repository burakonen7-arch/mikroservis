using Microsoft.Extensions.Configuration;
using RabbitMQ.Client;

namespace BuildingBlocks.Messaging;

public sealed class RabbitConnection(IConfiguration configuration) : IAsyncDisposable
{
    private readonly SemaphoreSlim gate = new(1);
    private IConnection? connection;

    public async Task<IConnection> GetAsync(CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            if (connection is null || !connection.IsOpen)
            {
                if (connection is not null)
                    await connection.DisposeAsync();

                var uri = configuration["RabbitMQ:Uri"]
                    ?? throw new InvalidOperationException("RabbitMQ:Uri is required.");
                connection = await new ConnectionFactory
                {
                    Uri = new Uri(uri),
                    AutomaticRecoveryEnabled = true
                }.CreateConnectionAsync(cancellationToken);
            }

            return connection;
        }
        finally
        {
            gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (connection is not null)
            await connection.DisposeAsync();
        gate.Dispose();
    }
}

// RabbitConnection: RabbitMQ bağlantısını ihtiyaç olduğunda açar ve servis genelinde paylaşır.
// Eşzamanlı bağlantı açılmasını engeller, otomatik kurtarmayı etkinleştirir ve kapanışta kaynakları temizler.
