using MongoDB.Driver;

namespace Graphode.BillingEntitlementsService.Infrastructure.Persistence.ReferenceItems;

public sealed class ReferenceItemCollectionInitializer(IMongoCollectionAccessor collectionAccessor) : IMongoCollectionInitializer
{
    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        var collection = collectionAccessor.GetCollection<ReferenceItemDocument>(ReferenceItemRepository.CollectionName);
        var models = new[]
        {
            new CreateIndexModel<ReferenceItemDocument>(
                Builders<ReferenceItemDocument>.IndexKeys
                    .Ascending(document => document.WorkspaceId)
                    .Ascending(document => document.Status)
                    .Descending(document => document.CreatedAtUtc)),
            new CreateIndexModel<ReferenceItemDocument>(
                Builders<ReferenceItemDocument>.IndexKeys
                    .Ascending(document => document.Name))
        };

        await collection.Indexes.CreateManyAsync(models, cancellationToken);
    }
}
