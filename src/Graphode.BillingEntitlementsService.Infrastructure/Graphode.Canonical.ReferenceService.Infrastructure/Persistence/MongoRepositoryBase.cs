using MongoDB.Driver;

namespace Graphode.BillingEntitlementsService.Infrastructure.Persistence;

public interface IMongoDocument
{
    string Id { get; }
}

public abstract class MongoRepositoryBase<TDocument>(IMongoCollection<TDocument> collection)
    where TDocument : IMongoDocument
{
    protected IMongoCollection<TDocument> Collection { get; } = collection;

    protected async Task<TDocument?> GetByIdAsync(string id, CancellationToken cancellationToken) =>
        await Collection.Find(document => document.Id == id).FirstOrDefaultAsync(cancellationToken);

    protected Task<bool> ExistsAsync(string id, CancellationToken cancellationToken) =>
        Collection.Find(document => document.Id == id).AnyAsync(cancellationToken);

    protected Task InsertAsync(TDocument document, CancellationToken cancellationToken) =>
        Collection.InsertOneAsync(document, cancellationToken: cancellationToken);

    protected Task ReplaceAsync(TDocument document, CancellationToken cancellationToken) =>
        Collection.ReplaceOneAsync(existing => existing.Id == document.Id, document, cancellationToken: cancellationToken);

    protected Task DeleteAsync(string id, CancellationToken cancellationToken) =>
        Collection.DeleteOneAsync(document => document.Id == id, cancellationToken);
}
