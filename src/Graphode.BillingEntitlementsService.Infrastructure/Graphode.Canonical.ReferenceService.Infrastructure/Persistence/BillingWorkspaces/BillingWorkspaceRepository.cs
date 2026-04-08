using Graphode.BillingEntitlementsService.Application.Abstractions.Persistence;
using Graphode.BillingEntitlementsService.Application.Services;
using MongoDB.Driver;

namespace Graphode.BillingEntitlementsService.Infrastructure.Persistence.BillingWorkspaces;

public sealed class BillingWorkspaceRepository(IMongoCollectionAccessor collectionAccessor)
    : MongoRepositoryBase<BillingWorkspaceDocument>(collectionAccessor.GetCollection<BillingWorkspaceDocument>(CollectionName)),
      IBillingWorkspaceRepository
{
    public const string CollectionName = "billing_workspaces";

    public BillingWorkspaceSnapshot? GetByWorkspaceId(string workspaceId) =>
        Collection.Find(document => document.WorkspaceId == workspaceId).FirstOrDefault()?.ToSnapshot();

    public BillingWorkspaceSnapshot? FindByStripeCustomerId(string stripeCustomerId) =>
        Collection.Find(document => document.StripeCustomerId == stripeCustomerId).FirstOrDefault()?.ToSnapshot();

    public BillingWorkspaceSnapshot? FindByProviderSubscriptionId(string providerSubscriptionId) =>
        Collection.Find(document => document.Subscription != null && document.Subscription.ProviderSubscriptionId == providerSubscriptionId)
            .FirstOrDefault()
            ?.ToSnapshot();

    public void Upsert(BillingWorkspaceSnapshot snapshot)
    {
        var document = BillingWorkspaceDocument.FromSnapshot(snapshot);
        Collection.ReplaceOne(
            existing => existing.Id == snapshot.WorkspaceId,
            document,
            new ReplaceOptions { IsUpsert = true });
    }
}
