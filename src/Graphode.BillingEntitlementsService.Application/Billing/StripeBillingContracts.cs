using Graphode.BillingEntitlementsService.Domain.Billing;

namespace Graphode.BillingEntitlementsService.Application.Services;

public interface IBillingStripeClient
{
    StripeCustomerResult EnsureCustomer(BillingAccount account);

    string ResolvePriceId(PlanDefinition plan);

    StripeSetupIntentResult CreateSetupIntent(BillingAccount account);

    StripePaymentMethodResult ConfirmPaymentMethod(
        BillingAccount account,
        string setupIntentId,
        string stripePaymentMethodId);

    StripeSubscriptionResult CreateSubscription(
        BillingAccount account,
        PlanDefinition plan,
        string? defaultPaymentMethodId);

    StripeSubscriptionResult ChangeSubscription(
        BillingAccount account,
        Subscription subscription,
        PlanDefinition plan,
        string? defaultPaymentMethodId);

    StripeSubscriptionResult CancelSubscription(Subscription subscription);

    StripeWebhookEventResult ParseWebhookEvent(string payload, string signatureHeader);
}

public sealed record StripeCustomerResult(string StripeCustomerId);

public sealed record StripeSetupIntentResult(
    string StripeCustomerId,
    string SetupIntentId,
    string ClientSecret);

public sealed record StripePaymentMethodResult(
    string StripeCustomerId,
    string StripePaymentMethodId,
    string Type,
    string? Brand,
    string? Last4,
    int? ExpMonth,
    int? ExpYear);

public sealed record StripeSubscriptionResult(
    string StripeSubscriptionId,
    string StripeSubscriptionItemId,
    SubscriptionStatus Status,
    DateTimeOffset CurrentPeriodStartUtc,
    DateTimeOffset CurrentPeriodEndUtc,
    bool CancelAtPeriodEnd);

public sealed record StripeWebhookEventResult(
    string EventId,
    string EventType,
    string? StripeCustomerId,
    string? StripeSubscriptionId,
    string? StripeSubscriptionItemId,
    string? StripePriceId,
    SubscriptionStatus? SubscriptionStatus,
    DateTimeOffset? CurrentPeriodStartUtc,
    DateTimeOffset? CurrentPeriodEndUtc,
    bool? CancelAtPeriodEnd);
