using Graphode.BillingEntitlementsService.Infrastructure.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Graphode.BillingEntitlementsService.Infrastructure.Messaging;

public sealed class RabbitMqCommandConsumerHostedService(
    IRabbitMqConnectionProvider connectionProvider,
    RabbitMqJsonSerializer serializer,
    IEnumerable<ICommandHandlerInvoker> handlers,
    IOptions<RabbitMqOptions> options,
    ILogger<RabbitMqCommandConsumerHostedService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var connection = await connectionProvider.GetConnectionAsync(stoppingToken);
        var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);
        var settings = options.Value;

        await channel.ExchangeDeclareAsync(settings.CommandExchange, ExchangeType.Topic, durable: true, autoDelete: false, cancellationToken: stoppingToken);

        Dictionary<string, object?>? queueArguments = null;
        if (!string.IsNullOrWhiteSpace(settings.DeadLetterExchange))
        {
            queueArguments = new Dictionary<string, object?>
            {
                ["x-dead-letter-exchange"] = settings.DeadLetterExchange
            };
        }

        await channel.QueueDeclareAsync(settings.CommandQueue, durable: true, exclusive: false, autoDelete: false, arguments: queueArguments, cancellationToken: stoppingToken);
        await channel.BasicQosAsync(0, settings.PrefetchCount, false, stoppingToken);

        var handlerMap = handlers.ToDictionary(handler => handler.CommandType, StringComparer.Ordinal);
        foreach (var commandType in handlerMap.Keys)
        {
            await channel.QueueBindAsync(
                settings.CommandQueue,
                settings.CommandExchange,
                RabbitMqPublisher.ToRoutingKey(commandType),
                arguments: null,
                cancellationToken: stoppingToken);
        }

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (_, eventArgs) =>
        {
            try
            {
                var rawEnvelope = serializer.Deserialize<RawCommandEnvelope>(eventArgs.Body);

                using var scope = logger.BeginScope(new Dictionary<string, object?>
                {
                    ["correlationId"] = eventArgs.BasicProperties?.CorrelationId,
                    ["messageId"] = rawEnvelope.Id,
                    ["commandType"] = rawEnvelope.Type
                });

                if (!handlerMap.TryGetValue(rawEnvelope.Type, out var handler))
                {
                    logger.LogError("No command handler registered for {CommandType}.", rawEnvelope.Type);
                    await channel.BasicNackAsync(eventArgs.DeliveryTag, false, requeue: false, stoppingToken);
                    return;
                }

                await handler.HandleAsync(eventArgs.Body, stoppingToken);
                await channel.BasicAckAsync(eventArgs.DeliveryTag, false, stoppingToken);
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Command processing failed.");
                await channel.BasicNackAsync(eventArgs.DeliveryTag, false, requeue: false, stoppingToken);
            }
        };

        await channel.BasicConsumeAsync(settings.CommandQueue, autoAck: false, consumer: consumer, cancellationToken: stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
        }
    }
}
