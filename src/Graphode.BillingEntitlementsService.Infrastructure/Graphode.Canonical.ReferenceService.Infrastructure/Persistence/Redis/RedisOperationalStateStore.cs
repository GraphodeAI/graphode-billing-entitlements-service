using System.Text.Json;
using Graphode.BillingEntitlementsService.Application.Abstractions.Persistence;
using Graphode.BillingEntitlementsService.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace Graphode.BillingEntitlementsService.Infrastructure.Persistence.Redis;

public sealed class RedisOperationalStateStore(
    IRedisDatabaseAccessor redisDatabaseAccessor,
    IOptions<RedisOptions> options) : IOperationalStateStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public Task SetAsync<TValue>(string category, string key, TValue value, TimeSpan ttl, CancellationToken cancellationToken)
    {
        var redisKey = BuildKey(category, key);
        var payload = JsonSerializer.Serialize(value, SerializerOptions);
        var effectiveTtl = ttl == TimeSpan.Zero ? TimeSpan.FromMinutes(options.Value.OperationalStateTtlMinutes) : ttl;

        return redisDatabaseAccessor.Database.StringSetAsync(redisKey, payload, effectiveTtl);
    }

    public async Task<TValue?> GetAsync<TValue>(string category, string key, CancellationToken cancellationToken)
    {
        var redisKey = BuildKey(category, key);
        var value = await redisDatabaseAccessor.Database.StringGetAsync(redisKey);
        if (!value.HasValue)
        {
            return default;
        }

        return JsonSerializer.Deserialize<TValue>(value.ToString(), SerializerOptions);
    }

    public Task RemoveAsync(string category, string key, CancellationToken cancellationToken)
    {
        var redisKey = BuildKey(category, key);
        return redisDatabaseAccessor.Database.KeyDeleteAsync(redisKey);
    }

    private static string BuildKey(string category, string key) => $"{category}:{key}";
}
