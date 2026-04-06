using Graphode.BillingEntitlementsService.Contracts.Messaging;
using Graphode.BillingEntitlementsService.Application.Abstractions.Messaging;
using Microsoft.Extensions.DependencyInjection;

namespace Graphode.BillingEntitlementsService.Infrastructure.Messaging;

public interface ICommandHandlerInvoker
{
    string CommandType { get; }

    Task HandleAsync(ReadOnlyMemory<byte> body, CancellationToken cancellationToken);
}

public sealed class CommandHandlerInvoker<TPayload>(
    string commandType,
    RabbitMqJsonSerializer serializer,
    IServiceScopeFactory scopeFactory,
    Func<IServiceScope, ICommandHandler<TPayload>> handlerFactory)
    : ICommandHandlerInvoker
{
    public string CommandType => commandType;

    public async Task HandleAsync(ReadOnlyMemory<byte> body, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var handler = handlerFactory(scope);
        var command = serializer.Deserialize<CommandEnvelope<TPayload>>(body);
        await handler.HandleAsync(command, cancellationToken);
    }
}
