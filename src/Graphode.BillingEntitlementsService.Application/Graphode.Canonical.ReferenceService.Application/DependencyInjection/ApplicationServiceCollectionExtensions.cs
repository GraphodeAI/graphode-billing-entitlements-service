using Graphode.BillingEntitlementsService.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Graphode.BillingEntitlementsService.Application.DependencyInjection;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddBillingEntitlementsApplication(this IServiceCollection services)
    {
        services.AddSingleton<IBillingCatalogStore, BillingCatalogStore>();
        services.AddSingleton<BillingWorkspaceService>();
        return services;
    }
}
