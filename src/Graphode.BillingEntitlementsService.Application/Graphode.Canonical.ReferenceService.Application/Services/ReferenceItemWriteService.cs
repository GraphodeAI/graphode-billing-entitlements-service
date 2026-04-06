using Graphode.BillingEntitlementsService.Contracts.ReferenceItems;
using Graphode.BillingEntitlementsService.Application.Abstractions.Messaging;
using Graphode.BillingEntitlementsService.Application.Abstractions.Persistence;
using ApplicationExecutionContext = Graphode.BillingEntitlementsService.Application.Context.ExecutionContext;
using Graphode.BillingEntitlementsService.Domain.ReferenceItems;

namespace Graphode.BillingEntitlementsService.Application.Services;

public sealed class ReferenceItemWriteService(
    IReferenceItemRepository repository,
    IReferenceItemQueryCache cache,
    IEventPublisher eventPublisher,
    IPemPublisher pemPublisher,
    ICommandPublisher commandPublisher,
    ReferenceItemRequestValidator validator)
{
    public async Task<CreateReferenceItemResponse> CreateAsync(
        CreateReferenceItemRequest request,
        ApplicationExecutionContext executionContext,
        CancellationToken cancellationToken)
    {
        validator.ValidateForCreate(request);

        var timestampUtc = DateTimeOffset.UtcNow;
        var item = ReferenceItem.Create(
            Guid.NewGuid().ToString("N"),
            request.WorkspaceId,
            request.Name,
            request.Description,
            request.Tags.Select(tag => tag.Trim()).Where(tag => !string.IsNullOrWhiteSpace(tag)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            timestampUtc);

        await repository.InsertAsync(item, cancellationToken);
        await cache.InvalidateWorkspaceAsync(item.WorkspaceId, cancellationToken);

        await eventPublisher.PublishAsync(
            executionContext.CreateEventEnvelope(
                "ReferenceItemCreated",
                new ReferenceItemCreatedEventPayload(
                    item.Id,
                    item.WorkspaceId,
                    item.Name,
                    item.Status.ToString().ToLowerInvariant(),
                    item.CreatedAtUtc),
                timestampUtc),
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
                    timestampUtc),
                timestampUtc),
            cancellationToken);

        return new CreateReferenceItemResponse(item.Id, item.WorkspaceId, item.Status.ToString().ToLowerInvariant(), item.CreatedAtUtc);
    }

    public async Task DispatchArchiveAsync(
        ArchiveReferenceItemCommandRequest request,
        ApplicationExecutionContext executionContext,
        CancellationToken cancellationToken)
    {
        validator.ValidateForArchiveDispatch(request);

        var timestampUtc = DateTimeOffset.UtcNow;
        var payload = new ArchiveReferenceItemCommandPayload(request.ReferenceItemId, request.WorkspaceId, request.Reason);
        var envelope = executionContext.CreateCommandEnvelope("ReferenceItemArchiveRequested", payload, timestampUtc);

        await commandPublisher.PublishAsync(envelope, cancellationToken);
    }
}
