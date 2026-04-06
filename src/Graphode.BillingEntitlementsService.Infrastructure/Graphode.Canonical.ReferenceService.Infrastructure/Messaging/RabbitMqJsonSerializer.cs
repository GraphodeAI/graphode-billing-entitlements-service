using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Graphode.BillingEntitlementsService.Infrastructure.Messaging;

public sealed class RabbitMqJsonSerializer
{
    private readonly JsonSerializerOptions _options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public RabbitMqJsonSerializer()
    {
        _options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
    }

    public ReadOnlyMemory<byte> Serialize<T>(T value) =>
        Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value, _options));

    public T Deserialize<T>(ReadOnlyMemory<byte> body) =>
        JsonSerializer.Deserialize<T>(body.Span, _options)
        ?? throw new InvalidOperationException($"Unable to deserialize message body to {typeof(T).Name}.");
}
