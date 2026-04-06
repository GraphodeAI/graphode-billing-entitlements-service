namespace Graphode.BillingEntitlementsService.Contracts.Messaging;

public sealed record ActorContext(
    string ActorId,
    string ActorType,
    string? DisplayName);

public sealed record WorkspaceContext(
    string WorkspaceId,
    string? TenantId);

public sealed record CorrelationContext(
    string CorrelationId,
    string? CausationId,
    DateTimeOffset TimestampUtc,
    string Source);

public abstract record MessageEnvelope<TPayload>(
    string Id,
    string Type,
    string SchemaVersion,
    CorrelationContext Correlation,
    ActorContext Actor,
    WorkspaceContext Workspace,
    IReadOnlyDictionary<string, string> Metadata,
    TPayload Payload);

public sealed record CommandEnvelope<TPayload>(
    string Id,
    string Type,
    string SchemaVersion,
    CorrelationContext Correlation,
    ActorContext Actor,
    WorkspaceContext Workspace,
    IReadOnlyDictionary<string, string> Metadata,
    TPayload Payload)
    : MessageEnvelope<TPayload>(Id, Type, SchemaVersion, Correlation, Actor, Workspace, Metadata, Payload);

public sealed record EventEnvelope<TPayload>(
    string Id,
    string Type,
    string SchemaVersion,
    CorrelationContext Correlation,
    ActorContext Actor,
    WorkspaceContext Workspace,
    IReadOnlyDictionary<string, string> Metadata,
    TPayload Payload)
    : MessageEnvelope<TPayload>(Id, Type, SchemaVersion, Correlation, Actor, Workspace, Metadata, Payload);

public sealed record PemEnvelope<TPayload>(
    string Id,
    string Type,
    string SchemaVersion,
    CorrelationContext Correlation,
    ActorContext Actor,
    WorkspaceContext Workspace,
    IReadOnlyDictionary<string, string> Metadata,
    TPayload Payload)
    : MessageEnvelope<TPayload>(Id, Type, SchemaVersion, Correlation, Actor, Workspace, Metadata, Payload);
