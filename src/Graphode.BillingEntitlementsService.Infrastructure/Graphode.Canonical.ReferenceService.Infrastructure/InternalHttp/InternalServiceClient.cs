using Graphode.BillingEntitlementsService.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace Graphode.BillingEntitlementsService.Infrastructure.InternalHttp;

public sealed class InternalServiceClient(
    HttpClient httpClient,
    IOptions<InternalServiceClientOptions> options) : IInternalServiceClient
{
    public Task<HttpResponseMessage> SendAsync(string serviceName, HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (!options.Value.Services.TryGetValue(serviceName, out var baseAddress))
        {
            throw new InvalidOperationException($"Internal service '{serviceName}' is not configured.");
        }

        request.RequestUri = new Uri(new Uri(baseAddress, UriKind.Absolute), request.RequestUri ?? new Uri("/", UriKind.Relative));
        return httpClient.SendAsync(request, cancellationToken);
    }
}
