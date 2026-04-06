using Graphode.BillingEntitlementsService.Contracts.ReferenceItems;
using Graphode.BillingEntitlementsService.Application.Abstractions.Messaging;
using Graphode.BillingEntitlementsService.Application.Abstractions.Persistence;
using Graphode.BillingEntitlementsService.Contracts.Messaging;
using ApplicationExecutionContext = Graphode.BillingEntitlementsService.Application.Context.ExecutionContext;

namespace Graphode.BillingEntitlementsService.Application.Services;

public sealed class ArchiveReferenceItemCommandHandler(
    IReferenceItemRepository repository,
    IReferenceItemQueryCache cache,
    IEventPublisher eventPublisher,
    IPemPublisher pemPublisher)
    : ICommandHandler<ArchiveReferenceItemCommandPayload>
{
    public string CommandType => "ReferenceItemArchiveRequested";

    public async Task HandleAsync(CommandEnvelope<ArchiveReferenceItemCommandPayload> command, CancellationToken cancellationToken)
    {
        var item = await repository.GetByIdAsync(command.Payload.ReferenceItemId, cancellationToken);
        if (item is null)
        {
            return;
        }

        var archivedAtUtc = DateTimeOffset.UtcNow;
        var stateChanged = item.Archive(archivedAtUtc, command.Payload.Reason);
        if (!stateChanged)
        {
            return;
        }

        await repository.ReplaceAsync(item, cancellationToken);
        await cache.InvalidateWorkspaceAsync(item.WorkspaceId, cancellationToken);

        var executionContext = new ApplicationExecutionContext(
            command.Correlation.CorrelationId,
            command.Id,
            command.Actor.ActorId,
            command.Actor.ActorType,
            command.Actor.DisplayName,
            command.Workspace.WorkspaceId,
            command.Workspace.TenantId,
            command.Correlation.Source);

        await eventPublisher.PublishAsync(
            executionContext.CreateEventEnvelope(
                "ReferenceItemArchived",
                new ReferenceItemArchivedEventPayload(
                    item.Id,
                    item.WorkspaceId,
                    item.Status.ToString().ToLowerInvariant(),
                    command.Payload.Reason,
                    archivedAtUtc),
                archivedAtUtc),
            cancellationToken);

        await pemPublisher.PublishAsync(
            executionContext.CreatePemEnvelope(
                "ReferenceItemPlatformEventModel",
                new ReferenceItemPlatformEventModelPayload(
                    item.Id,
                    item.WorkspaceId,
                    item.Name,
                    item.Status.ToString().ToLowerInvariant(),
                    item.Description,
                    item.Tags,
                    archivedAtUtc),
                archivedAtUtc),
            cancellationToken);
    }
}
