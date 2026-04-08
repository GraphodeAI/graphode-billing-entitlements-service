using Graphode.BillingEntitlementsService.Application.Abstractions.Persistence;
using Graphode.BillingEntitlementsService.Infrastructure.Configuration;
using Graphode.BillingEntitlementsService.Infrastructure.Health;
using Graphode.BillingEntitlementsService.Infrastructure.Persistence;
using Graphode.BillingEntitlementsService.Infrastructure.Persistence.BillingWorkspaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace Graphode.BillingEntitlementsService.Infrastructure.DependencyInjection;

public static class BillingServiceCollectionExtensions
{
    public static IServiceCollection AddBillingPersistenceInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddOptions<MongoDbOptions>()
            .Bind(configuration.GetSection(MongoDbOptions.SectionName))
            .ValidateOnStart();

        services.AddSingleton<IMongoClient>(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<MongoDbOptions>>().Value;
            return new MongoClient(options.ConnectionString);
        });

        services.AddSingleton(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<MongoDbOptions>>().Value;
            var client = serviceProvider.GetRequiredService<IMongoClient>();
            return client.GetDatabase(options.DatabaseName);
        });

        services.AddSingleton<IMongoCollectionAccessor, MongoCollectionAccessor>();
        services.AddSingleton<IMongoCollectionInitializer, BillingWorkspaceCollectionInitializer>();
        services.AddHostedService<MongoCollectionInitializationHostedService>();
        services.AddSingleton<IBillingWorkspaceRepository, BillingWorkspaceRepository>();

        services.AddHealthChecks()
            .AddCheck<MongoDbHealthCheck>("mongo");

        return services;
    }
}
