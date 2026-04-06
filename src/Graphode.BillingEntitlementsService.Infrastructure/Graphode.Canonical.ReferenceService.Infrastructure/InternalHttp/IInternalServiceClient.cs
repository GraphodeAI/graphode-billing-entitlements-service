namespace Graphode.BillingEntitlementsService.Infrastructure.InternalHttp;

public interface IInternalServiceClient
{
    Task<HttpResponseMessage> SendAsync(string serviceName, HttpRequestMessage request, CancellationToken cancellationToken);
}
