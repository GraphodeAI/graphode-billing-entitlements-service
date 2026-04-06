using MongoDB.Driver;

namespace Graphode.BillingEntitlementsService.Infrastructure.Persistence;

public interface IMongoCollectionAccessor
{
    IMongoCollection<TDocument> GetCollection<TDocument>(string name);
}
