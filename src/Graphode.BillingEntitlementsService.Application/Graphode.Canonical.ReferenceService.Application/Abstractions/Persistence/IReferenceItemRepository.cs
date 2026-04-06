using Graphode.BillingEntitlementsService.Contracts.Common;
using Graphode.BillingEntitlementsService.Domain.ReferenceItems;
using Graphode.BillingEntitlementsService.Contracts.ReferenceItems;

namespace Graphode.BillingEntitlementsService.Application.Abstractions.Persistence;

public sealed record ReferenceItemReadCriteria(
    int Page,
    int PageSize,
    IReadOnlyList<SortDescriptor> Sort,
    IReadOnlyList<FilterDescriptor> Filters);

public sealed record ReferenceItemQueryResult(
    IReadOnlyList<ReferenceItem> Items,
    long TotalCount);

public interface IReferenceItemRepository
{
    Task<ReferenceItem?> GetByIdAsync(string id, CancellationToken cancellationToken);

    Task<bool> ExistsAsync(string id, CancellationToken cancellationToken);

    Task InsertAsync(ReferenceItem item, CancellationToken cancellationToken);

    Task ReplaceAsync(ReferenceItem item, CancellationToken cancellationToken);

    Task DeleteAsync(string id, CancellationToken cancellationToken);

    Task<ReferenceItemQueryResult> ListAsync(ReferenceItemReadCriteria criteria, CancellationToken cancellationToken);
}

public interface IReferenceItemQueryCache
{
    Task<ListReferenceItemsResponse?> GetAsync(ListReferenceItemsRequest request, CancellationToken cancellationToken);

    Task SetAsync(ListReferenceItemsRequest request, ListReferenceItemsResponse response, CancellationToken cancellationToken);

    Task InvalidateWorkspaceAsync(string workspaceId, CancellationToken cancellationToken);
}

public interface IOperationalStateStore
{
    Task SetAsync<TValue>(string category, string key, TValue value, TimeSpan ttl, CancellationToken cancellationToken);

    Task<TValue?> GetAsync<TValue>(string category, string key, CancellationToken cancellationToken);

    Task RemoveAsync(string category, string key, CancellationToken cancellationToken);
}

public interface IRateLimitStateStore
{
    Task<long> IncrementAsync(string bucket, string key, TimeSpan ttl, CancellationToken cancellationToken);
}
