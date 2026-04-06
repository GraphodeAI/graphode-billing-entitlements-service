using RabbitMQ.Client;

namespace Graphode.BillingEntitlementsService.Infrastructure.Messaging;

public interface IRabbitMqConnectionProvider
{
    Task<IConnection> GetConnectionAsync(CancellationToken cancellationToken);
}
