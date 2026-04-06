using Graphode.BillingEntitlementsService.Contracts.ReferenceItems;
using Graphode.BillingEntitlementsService.Application.Abstractions.Messaging;
using Graphode.BillingEntitlementsService.Application.Abstractions.Persistence;
using Graphode.BillingEntitlementsService.Application.Services;
using Graphode.BillingEntitlementsService.Infrastructure.Configuration;
using Graphode.BillingEntitlementsService.Infrastructure.Health;
using Graphode.BillingEntitlementsService.Infrastructure.InternalHttp;
using Graphode.BillingEntitlementsService.Infrastructure.Messaging;
using Graphode.BillingEntitlementsService.Infrastructure.Persistence;
using Graphode.BillingEntitlementsService.Infrastructure.Persistence.Redis;
using Graphode.BillingEntitlementsService.Infrastructure.Persistence.ReferenceItems;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using StackExchange.Redis;

namespace Graphode.BillingEntitlementsService.Infrastructure.DependencyInjection;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddReferenceServiceInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddOptions<ServiceIdentityOptions>()
            .Bind(configuration.GetSection(ServiceIdentityOptions.SectionName))
            .ValidateOnStart();

        services
            .AddOptions<MongoDbOptions>()
            .Bind(configuration.GetSection(MongoDbOptions.SectionName))
            .ValidateOnStart();

        services
            .AddOptions<RabbitMqOptions>()
            .Bind(configuration.GetSection(RabbitMqOptions.SectionName))
            .ValidateOnStart();

        services
            .AddOptions<RedisOptions>()
            .Bind(configuration.GetSection(RedisOptions.SectionName))
            .ValidateOnStart();

        services
            .AddOptions<InternalServiceClientOptions>()
            .Bind(configuration.GetSection(InternalServiceClientOptions.SectionName))
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
        services.AddSingleton<IMongoCollectionInitializer, ReferenceItemCollectionInitializer>();
        services.AddHostedService<MongoCollectionInitializationHostedService>();

        services.AddScoped<IReferenceItemRepository, ReferenceItemRepository>();

        var redisOptions = configuration.GetSection(RedisOptions.SectionName).Get<RedisOptions>() ?? new RedisOptions();

        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = redisOptions.Configuration;
            options.InstanceName = redisOptions.InstanceName;
        });

        services.AddSingleton<IConnectionMultiplexer>(serviceProvider =>
        {
            var redis = serviceProvider.GetRequiredService<IOptions<RedisOptions>>().Value;
            return ConnectionMultiplexer.Connect(redis.Configuration);
        });
        services.AddSingleton<IRedisDatabaseAccessor, RedisDatabaseAccessor>();
        services.AddScoped<IReferenceItemQueryCache, ReferenceItemQueryCache>();
        services.AddScoped<IOperationalStateStore, RedisOperationalStateStore>();
        services.AddScoped<IRateLimitStateStore, RedisRateLimitStateStore>();

        services.AddSingleton<RabbitMqJsonSerializer>();
        services.AddSingleton<IRabbitMqConnectionProvider, RabbitMqConnectionProvider>();
        services.AddSingleton<RabbitMqPublisher>();
        services.AddSingleton<ICommandHandlerInvoker>(serviceProvider =>
            new CommandHandlerInvoker<ArchiveReferenceItemCommandPayload>(
                "ReferenceItemArchiveRequested",
                serviceProvider.GetRequiredService<RabbitMqJsonSerializer>(),
                serviceProvider.GetRequiredService<IServiceScopeFactory>(),
                static scope => scope.ServiceProvider.GetRequiredService<ArchiveReferenceItemCommandHandler>()));

        services.AddSingleton<ICommandPublisher>(serviceProvider => serviceProvider.GetRequiredService<RabbitMqPublisher>());
        services.AddSingleton<IEventPublisher>(serviceProvider => serviceProvider.GetRequiredService<RabbitMqPublisher>());
        services.AddSingleton<IPemPublisher>(serviceProvider => serviceProvider.GetRequiredService<RabbitMqPublisher>());
        services.AddHostedService<RabbitMqCommandConsumerHostedService>();

        services.AddHttpContextAccessor();
        services.AddTransient<InternalContextPropagationHandler>();
        services.AddHttpClient<IInternalServiceClient, InternalServiceClient>()
            .AddHttpMessageHandler<InternalContextPropagationHandler>();

        services.AddHealthChecks()
            .AddCheck<MongoDbHealthCheck>("mongo")
            .AddCheck<RabbitMqHealthCheck>("rabbitmq")
            .AddCheck<RedisHealthCheck>("redis");

        return services;
    }
}
