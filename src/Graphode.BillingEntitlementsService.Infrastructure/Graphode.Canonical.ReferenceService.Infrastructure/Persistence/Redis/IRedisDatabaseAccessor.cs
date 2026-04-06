using StackExchange.Redis;

namespace Graphode.BillingEntitlementsService.Infrastructure.Persistence.Redis;

public interface IRedisDatabaseAccessor
{
    IDatabase Database { get; }
}
