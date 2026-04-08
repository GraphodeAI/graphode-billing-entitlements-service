using Graphode.BillingEntitlementsService.Application.Services;
using Graphode.BillingEntitlementsService.Infrastructure.BillingStripe;
using Graphode.BillingEntitlementsService.Infrastructure.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Graphode.BillingEntitlementsService.Infrastructure.DependencyInjection;

public static class StripeServiceCollectionExtensions
{
    public static IServiceCollection AddBillingStripeInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<StripeOptions>()
            .Bind(configuration.GetSection(StripeOptions.SectionName));

        services.AddSingleton<IBillingStripeClient, StripeBillingClient>();

        return services;
    }
}
