using Microsoft.Extensions.Diagnostics.HealthChecks;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Graphode.BillingEntitlementsService.Infrastructure.Health;

public sealed class MongoDbHealthCheck(IMongoDatabase database) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await database.RunCommandAsync((Command<BsonDocument>)"{ ping: 1 }", cancellationToken: cancellationToken);
            return HealthCheckResult.Healthy("MongoDB is reachable.");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("MongoDB ping failed.", exception);
        }
    }
}
