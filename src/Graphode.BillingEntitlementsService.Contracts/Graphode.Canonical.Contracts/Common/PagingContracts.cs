using System.ComponentModel.DataAnnotations;

namespace Graphode.BillingEntitlementsService.Contracts.Common;

public class PageRequest
{
    [Range(1, int.MaxValue)]
    public int Page { get; init; } = 1;

    [Range(1, 200)]
    public int PageSize { get; init; } = 20;
}

public sealed record PageMetadata(
    int Page,
    int PageSize,
    long TotalCount,
    int TotalPages,
    bool HasNextPage,
    bool HasPreviousPage);

public sealed record PagedResponse<TItem>(
    IReadOnlyList<TItem> Items,
    long TotalCount,
    PageMetadata Page);
