using Graphode.BillingEntitlementsService.Contracts.Messaging;
using Graphode.BillingEntitlementsService.Contracts.ReferenceItems;

namespace Graphode.BillingEntitlementsService.Application.Context;

public sealed record ExecutionContext(
    string CorrelationId,
    string? CausationId,
    string ActorId,
    string ActorType,
    string? ActorDisplayName,
    string WorkspaceId,
    string? TenantId,
    string Source)
{
    public ActorContext ToActorContext() => new(ActorId, ActorType, ActorDisplayName);

    public WorkspaceContext ToWorkspaceContext() => new(WorkspaceId, TenantId);

    public CorrelationContext ToCorrelationContext(DateTimeOffset timestampUtc) =>
        new(CorrelationId, CausationId, timestampUtc, Source);

    public CommandEnvelope<TPayload> CreateCommandEnvelope<TPayload>(string type, TPayload payload, DateTimeOffset timestampUtc) =>
        new(
            Guid.NewGuid().ToString("N"),
            type,
            ReferenceItemContractVersions.Current,
            ToCorrelationContext(timestampUtc),
            ToActorContext(),
            ToWorkspaceContext(),
            BuildMetadata(),
            payload);

    public EventEnvelope<TPayload> CreateEventEnvelope<TPayload>(string type, TPayload payload, DateTimeOffset timestampUtc) =>
        new(
            Guid.NewGuid().ToString("N"),
            type,
            ReferenceItemContractVersions.Current,
            ToCorrelationContext(timestampUtc),
            ToActorContext(),
            ToWorkspaceContext(),
            BuildMetadata(),
            payload);

    public PemEnvelope<TPayload> CreatePemEnvelope<TPayload>(string type, TPayload payload, DateTimeOffset timestampUtc) =>
        new(
            Guid.NewGuid().ToString("N"),
            type,
            ReferenceItemContractVersions.Current,
            ToCorrelationContext(timestampUtc),
            ToActorContext(),
            ToWorkspaceContext(),
            BuildMetadata(),
            payload);

    private IReadOnlyDictionary<string, string> BuildMetadata() =>
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["workspaceId"] = WorkspaceId,
            ["source"] = Source
        };
}
