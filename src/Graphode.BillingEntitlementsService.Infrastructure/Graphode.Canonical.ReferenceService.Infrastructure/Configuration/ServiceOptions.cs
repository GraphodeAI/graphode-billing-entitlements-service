namespace Graphode.BillingEntitlementsService.Infrastructure.Configuration;

public sealed class ServiceIdentityOptions
{
    public const string SectionName = "ServiceIdentity";

    public string ServiceName { get; init; } = "graphode-billing-entitlements-service";

    public string ServiceVersion { get; init; } = "1.0.0";

    public string Environment { get; init; } = "Development";
}

public sealed class MongoDbOptions
{
    public const string SectionName = "MongoDb";

    public string ConnectionString { get; init; } = "mongodb://localhost:27017";

    public string DatabaseName { get; init; } = "graphode_canonical_reference";

    public bool AutoCreateIndexes { get; init; } = true;
}

public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";

    public string HostName { get; init; } = "localhost";

    public int Port { get; init; } = 5672;

    public string UserName { get; init; } = "guest";

    public string Password { get; init; } = "guest";

    public string VirtualHost { get; init; } = "/";

    public string CommandExchange { get; init; } = "graphode.commands";

    public string EventExchange { get; init; } = "graphode.events";

    public string PemExchange { get; init; } = "graphode.pem";

    public string CommandQueue { get; init; } = "graphode.billing-entitlements-service.commands";

    public string? DeadLetterExchange { get; init; }

    public ushort PrefetchCount { get; init; } = 8;
}

public sealed class RedisOptions
{
    public const string SectionName = "Redis";

    public string Configuration { get; init; } = "localhost:6379";

    public string InstanceName { get; init; } = "graphode:";

    public int QueryCacheTtlSeconds { get; init; } = 60;

    public int OperationalStateTtlMinutes { get; init; } = 60;
}

public sealed class InternalServiceClientOptions
{
    public const string SectionName = "InternalServices";

    public Dictionary<string, string> Services { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}
