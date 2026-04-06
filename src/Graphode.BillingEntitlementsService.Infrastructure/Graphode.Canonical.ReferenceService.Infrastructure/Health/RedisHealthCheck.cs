using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace Graphode.BillingEntitlementsService.Infrastructure.Health;

public sealed class RedisHealthCheck(IConnectionMultiplexer connectionMultiplexer) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var latency = await connectionMultiplexer.GetDatabase().PingAsync();
            return HealthCheckResult.Healthy($"Redis ping succeeded in {latency.TotalMilliseconds:N0} ms.");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("Redis ping failed.", exception);
        }
    }
}
