using Graphode.BillingEntitlementsService.Contracts.Messaging;
using Graphode.BillingEntitlementsService.Contracts.ReferenceItems;

namespace Graphode.BillingEntitlementsService.Contracts.ContractArtifacts;

public sealed record ContractArtifactDefinition(
    string ContractId,
    string FileName,
    string Description,
    Type ContractType);

public static class ContractArtifactCatalog
{
    public static IReadOnlyList<ContractArtifactDefinition> Definitions { get; } =
    [
        new("read.reference-items.request", "read.reference-items.request.schema.json", "Read request with paging, sorting and filtering.", typeof(ListReferenceItemsRequest)),
        new("read.reference-items.response", "read.reference-items.response.schema.json", "Read response page for reference items.", typeof(ListReferenceItemsResponse)),
        new("write.reference-items.create.request", "write.reference-items.create.request.schema.json", "Synchronous write request payload.", typeof(CreateReferenceItemRequest)),
        new("write.reference-items.create.response", "write.reference-items.create.response.schema.json", "Synchronous write response payload.", typeof(CreateReferenceItemResponse)),
        new("command.reference-items.archive.request", "command.reference-items.archive.request.schema.json", "REST request that dispatches an archive command.", typeof(ArchiveReferenceItemCommandRequest)),
        new("command.reference-items.archive.payload", "command.reference-items.archive.payload.schema.json", "Archive command payload.", typeof(ArchiveReferenceItemCommandPayload)),
        new("command.reference-items.archive.envelope", "command.reference-items.archive.envelope.schema.json", "Archive command envelope.", typeof(CommandEnvelope<ArchiveReferenceItemCommandPayload>)),
        new("event.reference-items.created.payload", "event.reference-items.created.payload.schema.json", "Reference item created event payload.", typeof(ReferenceItemCreatedEventPayload)),
        new("event.reference-items.created.envelope", "event.reference-items.created.envelope.schema.json", "Reference item created event envelope.", typeof(EventEnvelope<ReferenceItemCreatedEventPayload>)),
        new("event.reference-items.archived.payload", "event.reference-items.archived.payload.schema.json", "Reference item archived event payload.", typeof(ReferenceItemArchivedEventPayload)),
        new("event.reference-items.archived.envelope", "event.reference-items.archived.envelope.schema.json", "Reference item archived event envelope.", typeof(EventEnvelope<ReferenceItemArchivedEventPayload>)),
        new("pem.reference-items.platform-event-model.payload", "pem.reference-items.platform-event-model.payload.schema.json", "Platform Event Model payload.", typeof(ReferenceItemPlatformEventModelPayload)),
        new("pem.reference-items.platform-event-model.envelope", "pem.reference-items.platform-event-model.envelope.schema.json", "Platform Event Model envelope.", typeof(PemEnvelope<ReferenceItemPlatformEventModelPayload>))
    ];
}
