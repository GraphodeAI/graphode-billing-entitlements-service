using Graphode.BillingEntitlementsService.Application.Abstractions.Persistence;

namespace Graphode.BillingEntitlementsService.Infrastructure.Persistence.Redis;

public sealed class RedisRateLimitStateStore(IRedisDatabaseAccessor redisDatabaseAccessor) : IRateLimitStateStore
{
    public async Task<long> IncrementAsync(string bucket, string key, TimeSpan ttl, CancellationToken cancellationToken)
    {
        var redisKey = $"rate-limit:{bucket}:{key}";
        var value = await redisDatabaseAccessor.Database.StringIncrementAsync(redisKey);
        _ = redisDatabaseAccessor.Database.KeyExpireAsync(redisKey, ttl);
        return value;
    }
}
