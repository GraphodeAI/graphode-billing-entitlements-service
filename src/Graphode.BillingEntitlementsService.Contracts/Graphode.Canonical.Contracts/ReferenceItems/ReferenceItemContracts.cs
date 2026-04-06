using System.ComponentModel.DataAnnotations;
using Graphode.BillingEntitlementsService.Contracts.Common;

namespace Graphode.BillingEntitlementsService.Contracts.ReferenceItems;

public static class ReferenceItemContractVersions
{
    public const string Current = "1.0";
}

public sealed class ListReferenceItemsRequest : PageRequest
{
    public IReadOnlyList<SortDescriptor> Sort { get; init; } = [];

    public IReadOnlyList<FilterDescriptor> Filters { get; init; } = [];
}

public sealed record ReferenceItemListItemResponse(
    string Id,
    string WorkspaceId,
    string Name,
    string Status,
    string? Description,
    IReadOnlyList<string> Tags,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset? ArchivedAtUtc);

public sealed record ListReferenceItemsResponse(
    IReadOnlyList<ReferenceItemListItemResponse> Items,
    long TotalCount,
    PageMetadata Page);

public sealed class CreateReferenceItemRequest
{
    [Required]
    public string WorkspaceId { get; init; } = string.Empty;

    [Required]
    [MaxLength(120)]
    public string Name { get; init; } = string.Empty;

    [MaxLength(512)]
    public string? Description { get; init; }

    public IReadOnlyList<string> Tags { get; init; } = [];
}

public sealed record CreateReferenceItemResponse(
    string Id,
    string WorkspaceId,
    string Status,
    DateTimeOffset CreatedAtUtc);

public sealed class ArchiveReferenceItemCommandRequest
{
    [Required]
    public string ReferenceItemId { get; init; } = string.Empty;

    [Required]
    public string WorkspaceId { get; init; } = string.Empty;

    [MaxLength(256)]
    public string? Reason { get; init; }
}

public sealed record ArchiveReferenceItemCommandPayload(
    string ReferenceItemId,
    string WorkspaceId,
    string? Reason);

public sealed record ReferenceItemCreatedEventPayload(
    string ReferenceItemId,
    string WorkspaceId,
    string Name,
    string Status,
    DateTimeOffset CreatedAtUtc);

public sealed record ReferenceItemArchivedEventPayload(
    string ReferenceItemId,
    string WorkspaceId,
    string Status,
    string? Reason,
    DateTimeOffset ArchivedAtUtc);

// PEM means Platform Event Model in this baseline. It is a stable platform-facing
// event payload shape for downstream consumers and cross-service integrations.
public sealed record ReferenceItemPlatformEventModelPayload(
    string ReferenceItemId,
    string WorkspaceId,
    string Name,
    string Status,
    string? Description,
    IReadOnlyList<string> Tags,
    DateTimeOffset OccurredAtUtc);
