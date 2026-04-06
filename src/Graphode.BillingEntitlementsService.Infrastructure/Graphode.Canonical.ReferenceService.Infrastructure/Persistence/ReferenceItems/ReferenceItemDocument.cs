using Graphode.BillingEntitlementsService.Domain.ReferenceItems;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Graphode.BillingEntitlementsService.Infrastructure.Persistence.ReferenceItems;

public sealed class ReferenceItemDocument : IMongoDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public string Id { get; init; } = string.Empty;

    [BsonElement("workspaceId")]
    public string WorkspaceId { get; init; } = string.Empty;

    [BsonElement("name")]
    public string Name { get; init; } = string.Empty;

    [BsonElement("description")]
    [BsonIgnoreIfNull]
    public string? Description { get; init; }

    [BsonElement("tags")]
    public IReadOnlyList<string> Tags { get; init; } = [];

    [BsonElement("status")]
    public string Status { get; init; } = string.Empty;

    [BsonElement("createdAtUtc")]
    public DateTimeOffset CreatedAtUtc { get; init; }

    [BsonElement("updatedAtUtc")]
    public DateTimeOffset UpdatedAtUtc { get; init; }

    [BsonElement("archivedAtUtc")]
    [BsonIgnoreIfNull]
    public DateTimeOffset? ArchivedAtUtc { get; init; }

    public static ReferenceItemDocument FromDomain(ReferenceItem item) =>
        new()
        {
            Id = item.Id,
            WorkspaceId = item.WorkspaceId,
            Name = item.Name,
            Description = item.Description,
            Tags = item.Tags,
            Status = item.Status.ToString().ToLowerInvariant(),
            CreatedAtUtc = item.CreatedAtUtc,
            UpdatedAtUtc = item.UpdatedAtUtc,
            ArchivedAtUtc = item.ArchivedAtUtc
        };

    public ReferenceItem ToDomain()
    {
        var status = Enum.TryParse<ReferenceItemStatus>(Status, true, out var parsedStatus)
            ? parsedStatus
            : ReferenceItemStatus.Active;

        return ReferenceItem
            .Create(Id, WorkspaceId, Name, Description, Tags, CreatedAtUtc)
            .WithPersistenceState(UpdatedAtUtc, ArchivedAtUtc, status);
    }
}
