using StackExchange.Redis;

namespace Graphode.BillingEntitlementsService.Infrastructure.Persistence.Redis;

public sealed class RedisDatabaseAccessor(IConnectionMultiplexer connectionMultiplexer) : IRedisDatabaseAccessor
{
    public IDatabase Database { get; } = connectionMultiplexer.GetDatabase();
}
