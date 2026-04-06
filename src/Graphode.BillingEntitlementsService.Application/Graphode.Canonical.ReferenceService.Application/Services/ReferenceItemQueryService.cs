using Graphode.BillingEntitlementsService.Contracts.Common;
using Graphode.BillingEntitlementsService.Contracts.ReferenceItems;
using Graphode.BillingEntitlementsService.Application.Abstractions.Persistence;
using Graphode.BillingEntitlementsService.Domain.ReferenceItems;

namespace Graphode.BillingEntitlementsService.Application.Services;

public sealed class ReferenceItemQueryService(
    IReferenceItemRepository repository,
    IReferenceItemQueryCache cache,
    ReferenceItemRequestValidator validator)
{
    public async Task<ListReferenceItemsResponse> ListAsync(ListReferenceItemsRequest request, CancellationToken cancellationToken)
    {
        validator.ValidateForList(request);

        var cached = await cache.GetAsync(request, cancellationToken);
        if (cached is not null)
        {
            return cached;
        }

        var result = await repository.ListAsync(
            new ReferenceItemReadCriteria(request.Page, request.PageSize, request.Sort, request.Filters),
            cancellationToken);

        var totalPages = result.TotalCount == 0
            ? 0
            : (int)Math.Ceiling(result.TotalCount / (double)request.PageSize);

        var response = new ListReferenceItemsResponse(
            result.Items.Select(Map).ToArray(),
            result.TotalCount,
            new PageMetadata(
                request.Page,
                request.PageSize,
                result.TotalCount,
                totalPages,
                request.Page < totalPages,
                request.Page > 1));

        await cache.SetAsync(request, response, cancellationToken);
        return response;
    }

    private static ReferenceItemListItemResponse Map(ReferenceItem item) =>
        new(
            item.Id,
            item.WorkspaceId,
            item.Name,
            item.Status.ToString().ToLowerInvariant(),
            item.Description,
            item.Tags,
            item.CreatedAtUtc,
            item.UpdatedAtUtc,
            item.ArchivedAtUtc);
}
