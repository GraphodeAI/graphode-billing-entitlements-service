namespace Graphode.BillingEntitlementsService.Domain.ReferenceItems;

public enum ReferenceItemStatus
{
    Active = 0,
    Archived = 1
}

public sealed class ReferenceItem
{
    private ReferenceItem(
        string id,
        string workspaceId,
        string name,
        string? description,
        IReadOnlyList<string> tags,
        ReferenceItemStatus status,
        DateTimeOffset createdAtUtc,
        DateTimeOffset updatedAtUtc,
        DateTimeOffset? archivedAtUtc)
    {
        Id = id;
        WorkspaceId = workspaceId;
        Name = name;
        Description = description;
        Tags = tags;
        Status = status;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = updatedAtUtc;
        ArchivedAtUtc = archivedAtUtc;
    }

    public string Id { get; }

    public string WorkspaceId { get; }

    public string Name { get; private set; }

    public string? Description { get; private set; }

    public IReadOnlyList<string> Tags { get; private set; }

    public ReferenceItemStatus Status { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public DateTimeOffset? ArchivedAtUtc { get; private set; }

    public static ReferenceItem Create(
        string id,
        string workspaceId,
        string name,
        string? description,
        IReadOnlyList<string> tags,
        DateTimeOffset timestampUtc)
    {
        return new ReferenceItem(
            id,
            workspaceId,
            name.Trim(),
            string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            tags,
            ReferenceItemStatus.Active,
            timestampUtc,
            timestampUtc,
            null);
    }

    public bool Archive(DateTimeOffset timestampUtc, string? reason)
    {
        _ = reason;

        if (Status == ReferenceItemStatus.Archived)
        {
            return false;
        }

        Status = ReferenceItemStatus.Archived;
        ArchivedAtUtc = timestampUtc;
        UpdatedAtUtc = timestampUtc;
        return true;
    }

    public ReferenceItem WithPersistenceState(
        DateTimeOffset updatedAtUtc,
        DateTimeOffset? archivedAtUtc,
        ReferenceItemStatus status)
    {
        return new ReferenceItem(
            Id,
            WorkspaceId,
            Name,
            Description,
            Tags,
            status,
            CreatedAtUtc,
            updatedAtUtc,
            archivedAtUtc);
    }
}
