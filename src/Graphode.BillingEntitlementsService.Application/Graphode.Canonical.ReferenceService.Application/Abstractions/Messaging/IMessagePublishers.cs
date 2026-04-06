using Graphode.BillingEntitlementsService.Contracts.Messaging;

namespace Graphode.BillingEntitlementsService.Application.Abstractions.Messaging;

public interface ICommandPublisher
{
    Task PublishAsync<TPayload>(CommandEnvelope<TPayload> envelope, CancellationToken cancellationToken);
}

public interface IEventPublisher
{
    Task PublishAsync<TPayload>(EventEnvelope<TPayload> envelope, CancellationToken cancellationToken);
}

public interface IPemPublisher
{
    Task PublishAsync<TPayload>(PemEnvelope<TPayload> envelope, CancellationToken cancellationToken);
}

public interface ICommandHandler<TPayload>
{
    string CommandType { get; }

    Task HandleAsync(CommandEnvelope<TPayload> command, CancellationToken cancellationToken);
}
