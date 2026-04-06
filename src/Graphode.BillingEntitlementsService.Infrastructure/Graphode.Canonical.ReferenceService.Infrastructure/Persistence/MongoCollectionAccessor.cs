using MongoDB.Driver;

namespace Graphode.BillingEntitlementsService.Infrastructure.Persistence;

public sealed class MongoCollectionAccessor(IMongoDatabase database) : IMongoCollectionAccessor
{
    public IMongoCollection<TDocument> GetCollection<TDocument>(string name) => database.GetCollection<TDocument>(name);
}
