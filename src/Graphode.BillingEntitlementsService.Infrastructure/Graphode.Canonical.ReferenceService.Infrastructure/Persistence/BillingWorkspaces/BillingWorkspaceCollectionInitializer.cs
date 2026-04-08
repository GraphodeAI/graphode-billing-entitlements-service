using MongoDB.Driver;

namespace Graphode.BillingEntitlementsService.Infrastructure.Persistence.BillingWorkspaces;

public sealed class BillingWorkspaceCollectionInitializer(IMongoCollectionAccessor collectionAccessor) : IMongoCollectionInitializer
{
    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        var collection = collectionAccessor.GetCollection<BillingWorkspaceDocument>(BillingWorkspaceRepository.CollectionName);
        var models = new[]
        {
            new CreateIndexModel<BillingWorkspaceDocument>(
                Builders<BillingWorkspaceDocument>.IndexKeys
                    .Ascending(document => document.WorkspaceId),
                new CreateIndexOptions { Unique = true }),
            new CreateIndexModel<BillingWorkspaceDocument>(
                Builders<BillingWorkspaceDocument>.IndexKeys
                    .Ascending(document => document.StripeCustomerId)),
            new CreateIndexModel<BillingWorkspaceDocument>(
                Builders<BillingWorkspaceDocument>.IndexKeys
                    .Ascending(document => document.Subscription!.ProviderSubscriptionId))
        };

        await collection.Indexes.CreateManyAsync(models, cancellationToken);
    }
}
