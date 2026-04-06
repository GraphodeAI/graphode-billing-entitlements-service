using Graphode.BillingEntitlementsService.Infrastructure.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Graphode.BillingEntitlementsService.Infrastructure.Persistence;

public interface IMongoCollectionInitializer
{
    Task InitializeAsync(CancellationToken cancellationToken);
}

public sealed class MongoCollectionInitializationHostedService(
    IEnumerable<IMongoCollectionInitializer> initializers,
    IOptions<MongoDbOptions> options,
    ILogger<MongoCollectionInitializationHostedService> logger)
    : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!options.Value.AutoCreateIndexes)
        {
            logger.LogInformation("Mongo index initialization is disabled.");
            return;
        }

        foreach (var initializer in initializers)
        {
            await initializer.InitializeAsync(cancellationToken);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
