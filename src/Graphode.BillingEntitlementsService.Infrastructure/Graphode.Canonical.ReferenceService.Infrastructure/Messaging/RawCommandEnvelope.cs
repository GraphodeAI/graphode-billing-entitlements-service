using System.Text.Json;

namespace Graphode.BillingEntitlementsService.Infrastructure.Messaging;

public sealed class RawCommandEnvelope
{
    public string Id { get; init; } = string.Empty;

    public string Type { get; init; } = string.Empty;

    public string SchemaVersion { get; init; } = string.Empty;

    public JsonElement Payload { get; init; }
}
