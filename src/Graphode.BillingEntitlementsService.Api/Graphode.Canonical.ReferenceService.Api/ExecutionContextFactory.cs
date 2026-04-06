using ApplicationExecutionContext = Graphode.BillingEntitlementsService.Application.Context.ExecutionContext;

namespace Graphode.BillingEntitlementsService.Api;

internal static class ExecutionContextFactory
{
    public static ApplicationExecutionContext Create(HttpContext httpContext, string workspaceId, string source) =>
        new(
            GetOrCreateHeader(httpContext, "X-Correlation-Id"),
            httpContext.Request.Headers["X-Causation-Id"].FirstOrDefault(),
            httpContext.Request.Headers["X-Actor-Id"].FirstOrDefault() ?? "system",
            httpContext.Request.Headers["X-Actor-Type"].FirstOrDefault() ?? "user",
            httpContext.Request.Headers["X-Actor-Display-Name"].FirstOrDefault(),
            workspaceId,
            httpContext.Request.Headers["X-Tenant-Id"].FirstOrDefault(),
            source);

    private static string GetOrCreateHeader(HttpContext httpContext, string key)
    {
        if (httpContext.Request.Headers.TryGetValue(key, out var existing) && !string.IsNullOrWhiteSpace(existing.FirstOrDefault()))
        {
            httpContext.Response.Headers[key] = existing.ToString();
            return existing.ToString();
        }

        var generated = Guid.NewGuid().ToString("N");
        httpContext.Response.Headers[key] = generated;
        return generated;
    }
}
