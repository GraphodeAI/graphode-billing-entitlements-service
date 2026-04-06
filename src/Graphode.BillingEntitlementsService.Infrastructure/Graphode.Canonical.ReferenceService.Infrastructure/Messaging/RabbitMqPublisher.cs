using Graphode.BillingEntitlementsService.Contracts.Messaging;
using Graphode.BillingEntitlementsService.Application.Abstractions.Messaging;
using Graphode.BillingEntitlementsService.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Graphode.BillingEntitlementsService.Infrastructure.Messaging;

public sealed class RabbitMqPublisher(
    IRabbitMqConnectionProvider connectionProvider,
    RabbitMqJsonSerializer serializer,
    IOptions<RabbitMqOptions> options,
    ILogger<RabbitMqPublisher> logger)
    : ICommandPublisher, IEventPublisher, IPemPublisher
{
    public Task PublishAsync<TPayload>(CommandEnvelope<TPayload> envelope, CancellationToken cancellationToken) =>
        PublishAsync(options.Value.CommandExchange, envelope.Type, envelope.Id, envelope.Correlation.CorrelationId, envelope, cancellationToken);

    public Task PublishAsync<TPayload>(EventEnvelope<TPayload> envelope, CancellationToken cancellationToken) =>
        PublishAsync(options.Value.EventExchange, envelope.Type, envelope.Id, envelope.Correlation.CorrelationId, envelope, cancellationToken);

    public Task PublishAsync<TPayload>(PemEnvelope<TPayload> envelope, CancellationToken cancellationToken) =>
        PublishAsync(options.Value.PemExchange, envelope.Type, envelope.Id, envelope.Correlation.CorrelationId, envelope, cancellationToken);

    private async Task PublishAsync<TEnvelope>(
        string exchange,
        string messageType,
        string messageId,
        string correlationId,
        TEnvelope envelope,
        CancellationToken cancellationToken)
    {
        var connection = await connectionProvider.GetConnectionAsync(cancellationToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);

        await channel.ExchangeDeclareAsync(exchange, ExchangeType.Topic, durable: true, autoDelete: false, cancellationToken: cancellationToken);

        var properties = new BasicProperties
        {
            Persistent = true,
            ContentType = "application/json",
            Type = messageType,
            MessageId = messageId,
            CorrelationId = correlationId
        };

        var body = serializer.Serialize(envelope);
        var routingKey = ToRoutingKey(messageType);

        logger.LogInformation("Publishing message {MessageType} to exchange {Exchange} with routing key {RoutingKey}.", messageType, exchange, routingKey);
        await channel.BasicPublishAsync(exchange, routingKey, false, properties, body, cancellationToken);
    }

    public static string ToRoutingKey(string messageType)
    {
        var chars = new List<char>(messageType.Length + 8);

        for (var index = 0; index < messageType.Length; index++)
        {
            var character = messageType[index];

            if (char.IsUpper(character) && index > 0)
            {
                chars.Add('.');
            }

            chars.Add(char.ToLowerInvariant(character));
        }

        return new string(chars.ToArray());
    }
}
