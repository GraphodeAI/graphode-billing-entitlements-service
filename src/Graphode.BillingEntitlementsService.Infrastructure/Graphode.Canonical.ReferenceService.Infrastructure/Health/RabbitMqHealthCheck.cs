using Graphode.BillingEntitlementsService.Infrastructure.Messaging;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Graphode.BillingEntitlementsService.Infrastructure.Health;

public sealed class RabbitMqHealthCheck(IRabbitMqConnectionProvider connectionProvider) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var connection = await connectionProvider.GetConnectionAsync(cancellationToken);
            return connection.IsOpen
                ? HealthCheckResult.Healthy("RabbitMQ connection is open.")
                : HealthCheckResult.Unhealthy("RabbitMQ connection is closed.");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("RabbitMQ connection failed.", exception);
        }
    }
}
