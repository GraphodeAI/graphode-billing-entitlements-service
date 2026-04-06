using Graphode.BillingEntitlementsService.Infrastructure.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Graphode.BillingEntitlementsService.Infrastructure.InternalHttp;

public sealed class InternalContextPropagationHandler(
    IHttpContextAccessor httpContextAccessor,
    IOptions<ServiceIdentityOptions> serviceIdentityOptions)
    : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var context = httpContextAccessor.HttpContext;
        if (context is not null)
        {
            CopyHeader(context, request, "X-Correlation-Id");
            CopyHeader(context, request, "X-Causation-Id");
            CopyHeader(context, request, "X-Actor-Id");
            CopyHeader(context, request, "X-Actor-Type");
            CopyHeader(context, request, "X-Actor-Display-Name");
            CopyHeader(context, request, "X-Tenant-Id");
            CopyHeader(context, request, "X-Workspace-Id");
        }

        request.Headers.TryAddWithoutValidation("X-Internal-Caller", serviceIdentityOptions.Value.ServiceName);
        return base.SendAsync(request, cancellationToken);
    }

    private static void CopyHeader(HttpContext context, HttpRequestMessage request, string headerName)
    {
        var value = context.Request.Headers[headerName].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(value))
        {
            request.Headers.TryAddWithoutValidation(headerName, value);
        }
    }
}
