using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Graphode.BillingEntitlementsService.Contracts.ReferenceItems;
using Graphode.BillingEntitlementsService.Application.Abstractions.Persistence;
using Graphode.BillingEntitlementsService.Infrastructure.Configuration;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;

namespace Graphode.BillingEntitlementsService.Infrastructure.Persistence.Redis;

public sealed class ReferenceItemQueryCache(
    IDistributedCache cache,
    IRedisDatabaseAccessor redisDatabaseAccessor,
    IOptions<RedisOptions> options) : IReferenceItemQueryCache
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public async Task<ListReferenceItemsResponse?> GetAsync(ListReferenceItemsRequest request, CancellationToken cancellationToken)
    {
        var version = await GetWorkspaceVersionAsync(request, cancellationToken);
        var key = BuildCacheKey(request, version);
        var payload = await cache.GetStringAsync(key, cancellationToken);

        return payload is null
            ? null
            : JsonSerializer.Deserialize<ListReferenceItemsResponse>(payload, SerializerOptions);
    }

    public async Task SetAsync(ListReferenceItemsRequest request, ListReferenceItemsResponse response, CancellationToken cancellationToken)
    {
        var version = await GetWorkspaceVersionAsync(request, cancellationToken);
        var key = BuildCacheKey(request, version);
        var payload = JsonSerializer.Serialize(response, SerializerOptions);

        await cache.SetStringAsync(
            key,
            payload,
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(options.Value.QueryCacheTtlSeconds)
            },
            cancellationToken);
    }

    public Task InvalidateWorkspaceAsync(string workspaceId, CancellationToken cancellationToken)
    {
        var key = GetWorkspaceVersionKey(workspaceId);
        return redisDatabaseAccessor.Database.StringIncrementAsync(key);
    }

    private async Task<long> GetWorkspaceVersionAsync(ListReferenceItemsRequest request, CancellationToken cancellationToken)
    {
        var workspaceId = request.Filters
            .FirstOrDefault(filter => string.Equals(filter.Field, "workspaceId", StringComparison.OrdinalIgnoreCase))
            ?.Values.FirstOrDefault()
            ?? "global";

        var key = GetWorkspaceVersionKey(workspaceId);
        var version = await redisDatabaseAccessor.Database.StringGetAsync(key);
        return version.HasValue && long.TryParse(version.ToString(), out var parsed) ? parsed : 0L;
    }

    private static string BuildCacheKey(ListReferenceItemsRequest request, long version)
    {
        var json = JsonSerializer.Serialize(request, SerializerOptions);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
        return $"reference-items:list:v{version}:{hash}";
    }

    private static string GetWorkspaceVersionKey(string workspaceId) => $"reference-items:workspace-version:{workspaceId}";
}
