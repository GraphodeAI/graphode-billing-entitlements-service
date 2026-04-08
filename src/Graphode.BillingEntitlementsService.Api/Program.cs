using Graphode.BillingEntitlementsService.Application.DependencyInjection;
using Graphode.BillingEntitlementsService.Application.Services;
using Graphode.BillingEntitlementsService.Contracts.Billing;
using Graphode.BillingEntitlementsService.Infrastructure.DependencyInjection;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();
builder.Services.AddBillingPersistenceInfrastructure(builder.Configuration);
builder.Services.AddBillingEntitlementsApplication();
builder.Services.AddBillingStripeInfrastructure(builder.Configuration);
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.Use(async (context, next) =>
{
    var correlationId = context.Request.Headers["X-Correlation-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    context.Response.Headers["X-Correlation-Id"] = correlationId;
    using var scope = app.Logger.BeginScope(new Dictionary<string, object?>
    {
        ["correlationId"] = correlationId,
        ["path"] = context.Request.Path.Value
    });

    await next();
});

app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new
        {
            status = report.Status.ToString(),
            checks = report.Entries.ToDictionary(entry => entry.Key, entry => entry.Value.Status.ToString())
        });
    }
});

app.MapGet("/api/v1/billing/plans", async (BillingWorkspaceService service, CancellationToken cancellationToken) =>
{
    var plans = await service.ListPlansAsync(cancellationToken);
    return Results.Ok(new BillingPlansResponse(plans));
});

app.MapGet("/api/v1/workspaces/{workspaceId}/billing-account", async (
    string workspaceId,
    BillingWorkspaceService service,
    CancellationToken cancellationToken) =>
{
    var response = await service.GetWorkspaceBillingViewAsync(workspaceId, cancellationToken);
    return Results.Ok(response);
});

app.MapGet("/api/v1/workspaces/{workspaceId}/billing/ledger", async (
    string workspaceId,
    BillingWorkspaceService service,
    CancellationToken cancellationToken) =>
{
    var response = await service.GetWorkspaceLedgerViewAsync(workspaceId, cancellationToken);
    return Results.Ok(response);
});

app.MapGet("/api/v1/workspaces/{workspaceId}/billing/payment-methods", async (
    string workspaceId,
    BillingWorkspaceService service,
    CancellationToken cancellationToken) =>
{
    var response = await service.GetWorkspacePaymentMethodsAsync(workspaceId, cancellationToken);
    return Results.Ok(response);
});

app.MapPost("/api/v1/workspaces/{workspaceId}/billing/subscription/start", async (
    string workspaceId,
    StartSubscriptionCommandRequest request,
    BillingWorkspaceService service,
    CancellationToken cancellationToken) =>
{
    try
    {
        var response = await service.StartSubscriptionAsync(workspaceId, request, cancellationToken);
        return Results.Created($"/api/v1/workspaces/{workspaceId}/billing-account", response);
    }
    catch (InvalidOperationException exception)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["billingSubscription"] = new[] { exception.Message }
        });
    }
});

app.MapPost("/api/v1/workspaces/{workspaceId}/billing/subscription/change", async (
    string workspaceId,
    ChangeSubscriptionCommandRequest request,
    BillingWorkspaceService service,
    CancellationToken cancellationToken) =>
{
    try
    {
        var response = await service.ChangeSubscriptionAsync(workspaceId, request, cancellationToken);
        return Results.Ok(response);
    }
    catch (InvalidOperationException exception)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["billingSubscription"] = new[] { exception.Message }
        });
    }
});

app.MapPost("/api/v1/workspaces/{workspaceId}/billing/subscription/cancel", async (
    string workspaceId,
    CancelSubscriptionCommandRequest request,
    BillingWorkspaceService service,
    CancellationToken cancellationToken) =>
{
    var response = await service.CancelSubscriptionAsync(workspaceId, request, cancellationToken);
    return Results.Ok(response);
});

app.MapPost("/api/v1/workspaces/{workspaceId}/billing/payment-methods/setup-intent", async (
    string workspaceId,
    SetupPaymentMethodCommandRequest request,
    BillingWorkspaceService service,
    CancellationToken cancellationToken) =>
{
    try
    {
        var response = await service.CapturePaymentMethodAsync(workspaceId, request, cancellationToken);
        return Results.Ok(response);
    }
    catch (ArgumentOutOfRangeException exception)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["setupPaymentMethod"] = new[] { exception.Message }
        });
    }
    catch (ArgumentException exception)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["setupPaymentMethod"] = new[] { exception.Message }
        });
    }
});

app.MapPost("/api/v1/webhooks/stripe", async (
    HttpRequest request,
    BillingWorkspaceService service,
    CancellationToken cancellationToken) =>
{
    _ = cancellationToken;
    var signatureHeader = request.Headers["Stripe-Signature"].FirstOrDefault();
    if (string.IsNullOrWhiteSpace(signatureHeader))
    {
        return Results.BadRequest(new
        {
            error = "Stripe-Signature header is required."
        });
    }

    using var reader = new StreamReader(request.Body);
    var payload = await reader.ReadToEndAsync();
    if (string.IsNullOrWhiteSpace(payload))
    {
        return Results.BadRequest(new
        {
            error = "Stripe webhook payload is empty."
        });
    }

    try
    {
        var response = await service.HandleStripeWebhookAsync(payload, signatureHeader, cancellationToken);
        return Results.Ok(response);
    }
    catch (InvalidOperationException exception) when (exception.Message.Contains("webhook secret", StringComparison.OrdinalIgnoreCase))
    {
        return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
    }
    catch (Stripe.StripeException exception)
    {
        return Results.BadRequest(new
        {
            error = exception.Message
        });
    }
});

app.MapPost("/api/v1/workspaces/{workspaceId}/billing/ledger/reserve", async (
    string workspaceId,
    ReserveCreditsCommandRequest request,
    BillingWorkspaceService service,
    CancellationToken cancellationToken) =>
{
    try
    {
        var response = await service.ReserveCreditsAsync(workspaceId, request, cancellationToken);
        return Results.Ok(response);
    }
    catch (ArgumentOutOfRangeException exception)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["reserveCredits"] = new[] { exception.Message }
        });
    }
    catch (InvalidOperationException exception)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["reserveCredits"] = new[] { exception.Message }
        });
    }
});

app.MapPost("/api/v1/workspaces/{workspaceId}/billing/ledger/commit", async (
    string workspaceId,
    CommitCreditsCommandRequest request,
    BillingWorkspaceService service,
    CancellationToken cancellationToken) =>
{
    try
    {
        var response = await service.CommitCreditsAsync(workspaceId, request, cancellationToken);
        return Results.Ok(response);
    }
    catch (ArgumentOutOfRangeException exception)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["commitCredits"] = new[] { exception.Message }
        });
    }
    catch (InvalidOperationException exception)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["commitCredits"] = new[] { exception.Message }
        });
    }
});

app.MapPost("/api/v1/workspaces/{workspaceId}/billing/ledger/release", async (
    string workspaceId,
    ReleaseCreditsCommandRequest request,
    BillingWorkspaceService service,
    CancellationToken cancellationToken) =>
{
    try
    {
        var response = await service.ReleaseCreditsAsync(workspaceId, request, cancellationToken);
        return Results.Ok(response);
    }
    catch (ArgumentOutOfRangeException exception)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["releaseCredits"] = new[] { exception.Message }
        });
    }
    catch (InvalidOperationException exception)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["releaseCredits"] = new[] { exception.Message }
        });
    }
});

app.Run();

public partial class Program;
