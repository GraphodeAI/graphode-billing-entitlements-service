using Graphode.BillingEntitlementsService.Application.Services;
using Graphode.BillingEntitlementsService.Domain.Billing;
using Graphode.BillingEntitlementsService.Infrastructure.Configuration;
using Microsoft.Extensions.Options;
using Stripe;
using System.Collections.Concurrent;
using BillingSubscription = Graphode.BillingEntitlementsService.Domain.Billing.Subscription;

namespace Graphode.BillingEntitlementsService.Infrastructure.BillingStripe;

public sealed class StripeBillingClient : IBillingStripeClient
{
    private readonly IOptions<StripeOptions> _options;
    private readonly ConcurrentDictionary<string, string> _resolvedPriceIds = new(StringComparer.OrdinalIgnoreCase);

    public StripeBillingClient(IOptions<StripeOptions> options)
    {
        _options = options;
    }

    public StripeCustomerResult EnsureCustomer(BillingAccount account)
    {
        if (!string.IsNullOrWhiteSpace(account.StripeCustomerId))
        {
            return new StripeCustomerResult(account.StripeCustomerId);
        }

        var customerService = CreateCustomerService();
        var customer = customerService.CreateAsync(new CustomerCreateOptions
        {
            Name = account.WorkspaceId,
            Description = $"Graphode billing account {account.BillingAccountId}",
            Metadata = new Dictionary<string, string>
            {
                ["workspaceId"] = account.WorkspaceId,
                ["billingAccountId"] = account.BillingAccountId,
                ["ownerScopeType"] = account.OwnerScopeType.ToString()
            }
        }).GetAwaiter().GetResult();

        return new StripeCustomerResult(customer.Id);
    }

    public string ResolvePriceId(PlanDefinition plan)
    {
        var cacheKey = $"{plan.PlanKey}:{plan.BillingInterval}:{plan.Version}";
        return _resolvedPriceIds.GetOrAdd(cacheKey, _ => ResolveOrCreatePriceId(plan));
    }

    public StripeSetupIntentResult CreateSetupIntent(BillingAccount account)
    {
        var customer = EnsureCustomer(account);
        var setupIntentService = CreateSetupIntentService();
        var setupIntent = setupIntentService.CreateAsync(new SetupIntentCreateOptions
        {
            Customer = customer.StripeCustomerId,
            PaymentMethodTypes = ["card"],
            Usage = "off_session"
        }).GetAwaiter().GetResult();

        return new StripeSetupIntentResult(customer.StripeCustomerId, setupIntent.Id, setupIntent.ClientSecret ?? string.Empty);
    }

    public StripePaymentMethodResult ConfirmPaymentMethod(BillingAccount account, string setupIntentId, string stripePaymentMethodId)
    {
        var customer = EnsureCustomer(account);
        var setupIntentService = CreateSetupIntentService();
        var paymentMethodService = CreatePaymentMethodService();

        var setupIntent = setupIntentService.GetAsync(setupIntentId).GetAwaiter().GetResult();
        var resolvedPaymentMethodId = string.IsNullOrWhiteSpace(setupIntent.PaymentMethodId)
            ? stripePaymentMethodId
            : setupIntent.PaymentMethodId;

        var paymentMethod = paymentMethodService.GetAsync(resolvedPaymentMethodId).GetAwaiter().GetResult();
        if (!string.Equals(paymentMethod.CustomerId, customer.StripeCustomerId, StringComparison.Ordinal))
        {
            paymentMethodService.AttachAsync(resolvedPaymentMethodId, new PaymentMethodAttachOptions
            {
                Customer = customer.StripeCustomerId
            }).GetAwaiter().GetResult();
            paymentMethod = paymentMethodService.GetAsync(resolvedPaymentMethodId).GetAwaiter().GetResult();
        }

        return new StripePaymentMethodResult(
            customer.StripeCustomerId,
            paymentMethod.Id,
            paymentMethod.Type ?? "card",
            paymentMethod.Card?.Brand,
            paymentMethod.Card?.Last4,
            paymentMethod.Card is null ? null : (int?)paymentMethod.Card.ExpMonth,
            paymentMethod.Card is null ? null : (int?)paymentMethod.Card.ExpYear);
    }

    public StripeSubscriptionResult CreateSubscription(BillingAccount account, PlanDefinition plan, string? defaultPaymentMethodId)
    {
        var customer = EnsureCustomer(account);
        var subscriptionService = CreateSubscriptionService();
        var priceId = ResolvePriceId(plan);
        var subscription = subscriptionService.CreateAsync(new SubscriptionCreateOptions
        {
            Customer = customer.StripeCustomerId,
            Items =
            [
                new SubscriptionItemOptions
                {
                    Price = priceId
                }
            ],
            CollectionMethod = "charge_automatically",
            DefaultPaymentMethod = defaultPaymentMethodId,
            PaymentBehavior = "default_incomplete",
            Expand = ["latest_invoice.payment_intent"]
        }).GetAwaiter().GetResult();

        return ToResult(EnsureSubscriptionPaymentCompleted(subscription, defaultPaymentMethodId));
    }

    public StripeSubscriptionResult ChangeSubscription(BillingAccount account, BillingSubscription subscription, PlanDefinition plan, string? defaultPaymentMethodId)
    {
        _ = account;
        var subscriptionService = CreateSubscriptionService();
        var priceId = ResolvePriceId(plan);
        var updated = subscriptionService.UpdateAsync(subscription.ProviderSubscriptionId, new SubscriptionUpdateOptions
        {
            Items =
            [
                new SubscriptionItemOptions
                {
                    Id = subscription.StripeSubscriptionItemId,
                    Price = priceId
                }
            ],
            DefaultPaymentMethod = defaultPaymentMethodId,
            ProrationBehavior = "create_prorations",
            Expand = ["latest_invoice.payment_intent"]
        }).GetAwaiter().GetResult();

        return ToResult(EnsureSubscriptionPaymentCompleted(updated, defaultPaymentMethodId));
    }

    public StripeSubscriptionResult CancelSubscription(BillingSubscription subscription)
    {
        var subscriptionService = CreateSubscriptionService();
        var cancelled = subscriptionService.CancelAsync(subscription.ProviderSubscriptionId, new SubscriptionCancelOptions
        {
            InvoiceNow = false,
            Prorate = false
        }).GetAwaiter().GetResult();

        return ToResult(cancelled);
    }

    public StripeWebhookEventResult ParseWebhookEvent(string payload, string signatureHeader)
    {
        var webhookSecret = _options.Value.WebhookSecret;
        if (string.IsNullOrWhiteSpace(webhookSecret))
        {
            throw new InvalidOperationException("Stripe webhook secret is not configured.");
        }

        var stripeEvent = EventUtility.ConstructEvent(
            payload,
            signatureHeader,
            webhookSecret,
            DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            throwOnApiVersionMismatch: false);

        return stripeEvent.Data.Object switch
        {
            Stripe.Subscription subscription => MapSubscriptionWebhookEvent(stripeEvent, subscription),
            Invoice invoice => new StripeWebhookEventResult(
                stripeEvent.Id,
                stripeEvent.Type,
                invoice.CustomerId,
                invoice.Parent?.SubscriptionDetails?.SubscriptionId,
                null,
                null,
                MapInvoiceStatus(stripeEvent.Type, invoice.Status),
                null,
                null,
                null),
            _ => new StripeWebhookEventResult(
                stripeEvent.Id,
                stripeEvent.Type,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null)
        };
    }

    private static StripeWebhookEventResult MapSubscriptionWebhookEvent(Event stripeEvent, Stripe.Subscription subscription)
    {
        var subscriptionItem = subscription.Items.Data.FirstOrDefault();
        return new StripeWebhookEventResult(
            stripeEvent.Id,
            stripeEvent.Type,
            subscription.CustomerId,
            subscription.Id,
            subscriptionItem?.Id,
            subscriptionItem?.Price?.Id,
            MapStatus(subscription.Status),
            subscriptionItem is null ? null : new DateTimeOffset(subscriptionItem.CurrentPeriodStart),
            subscriptionItem is null ? null : new DateTimeOffset(subscriptionItem.CurrentPeriodEnd),
            subscription.CancelAtPeriodEnd);
    }

    private CustomerService CreateCustomerService()
    {
        StripeConfiguration.ApiKey = EnsureSecretKey();
        return new CustomerService();
    }

    private SetupIntentService CreateSetupIntentService()
    {
        StripeConfiguration.ApiKey = EnsureSecretKey();
        return new SetupIntentService();
    }

    private InvoiceService CreateInvoiceService()
    {
        StripeConfiguration.ApiKey = EnsureSecretKey();
        return new InvoiceService();
    }

    private PriceService CreatePriceService()
    {
        StripeConfiguration.ApiKey = EnsureSecretKey();
        return new PriceService();
    }

    private ProductService CreateProductService()
    {
        StripeConfiguration.ApiKey = EnsureSecretKey();
        return new ProductService();
    }

    private PaymentMethodService CreatePaymentMethodService()
    {
        StripeConfiguration.ApiKey = EnsureSecretKey();
        return new PaymentMethodService();
    }

    private SubscriptionService CreateSubscriptionService()
    {
        StripeConfiguration.ApiKey = EnsureSecretKey();
        return new SubscriptionService();
    }

    private string EnsureSecretKey()
    {
        var secretKey = _options.Value.SecretKey;
        if (string.IsNullOrWhiteSpace(secretKey))
        {
            throw new InvalidOperationException("Stripe secret key is not configured.");
        }

        return secretKey;
    }

    private static StripeSubscriptionResult ToResult(Stripe.Subscription subscription)
    {
        var item = subscription.Items.Data.FirstOrDefault();
        var currentPeriodStart = item?.CurrentPeriodStart ?? DateTime.UtcNow;
        var currentPeriodEnd = item?.CurrentPeriodEnd ?? DateTime.UtcNow;
        return new StripeSubscriptionResult(
            subscription.Id,
            item?.Id ?? string.Empty,
            MapStatus(subscription.Status),
            new DateTimeOffset(currentPeriodStart),
            new DateTimeOffset(currentPeriodEnd),
            subscription.CancelAtPeriodEnd);
    }

    private Stripe.Subscription EnsureSubscriptionPaymentCompleted(Stripe.Subscription subscription, string? defaultPaymentMethodId)
    {
        if (string.IsNullOrWhiteSpace(defaultPaymentMethodId))
        {
            return subscription;
        }

        if (subscription.LatestInvoice is not Invoice invoice)
        {
            return subscription;
        }

        if (string.Equals(invoice.Status, "paid", StringComparison.OrdinalIgnoreCase))
        {
            return subscription;
        }

        if (!string.Equals(invoice.Status, "open", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(invoice.Status, "draft", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(invoice.Status, "uncollectible", StringComparison.OrdinalIgnoreCase))
        {
            return subscription;
        }

        var invoiceService = CreateInvoiceService();
        var paidInvoice = invoiceService.PayAsync(invoice.Id, new InvoicePayOptions
        {
            PaymentMethod = defaultPaymentMethodId,
        }).GetAwaiter().GetResult();

        if (!string.Equals(paidInvoice.Status, "paid", StringComparison.OrdinalIgnoreCase))
        {
            return subscription;
        }

        var refreshedSubscription = CreateSubscriptionService().GetAsync(subscription.Id, new SubscriptionGetOptions
        {
            Expand = ["latest_invoice.payment_intent"]
        }).GetAwaiter().GetResult();

        return refreshedSubscription;
    }

    private static SubscriptionStatus MapStatus(string? status) =>
        status?.ToLowerInvariant() switch
        {
            "trialing" => SubscriptionStatus.Trialing,
            "active" => SubscriptionStatus.Active,
            "past_due" => SubscriptionStatus.PastDue,
            "canceled" => SubscriptionStatus.Cancelled,
            "incomplete" => SubscriptionStatus.Incomplete,
            _ => SubscriptionStatus.Incomplete
        };

    private static SubscriptionStatus? MapInvoiceStatus(string eventType, string? status) =>
        eventType.ToLowerInvariant() switch
        {
            "invoice.payment_failed" => SubscriptionStatus.PastDue,
            "invoice.payment_succeeded" => SubscriptionStatus.Active,
            "invoice.paid" when string.Equals(status, "paid", StringComparison.OrdinalIgnoreCase) => SubscriptionStatus.Active,
            "invoice.finalized" => null,
            _ => null
        };

    private string ResolveOrCreatePriceId(PlanDefinition plan)
    {
        try
        {
            var existing = CreatePriceService().GetAsync(plan.StripePriceId).GetAwaiter().GetResult();
            return existing.Id;
        }
        catch (StripeException exception) when (exception.Message.Contains("No such price", StringComparison.OrdinalIgnoreCase))
        {
            // Fall through to on-demand provisioning for local/test environments.
        }

        var product = CreateProductService().CreateAsync(new ProductCreateOptions
        {
            Name = $"Graphode {plan.PlanKey}",
            Description = $"Graphode plan {plan.PlanKey} v{plan.Version}",
            Metadata = new Dictionary<string, string>
            {
                ["planKey"] = plan.PlanKey,
                ["planVersion"] = plan.Version.ToString(),
                ["billingInterval"] = plan.BillingInterval.ToString(),
                ["family"] = plan.Family.ToString()
            }
        }).GetAwaiter().GetResult();

        var unitAmount = (long)Math.Round(plan.BasePrice * 100m, MidpointRounding.AwayFromZero);
        var price = CreatePriceService().CreateAsync(new PriceCreateOptions
        {
            Product = product.Id,
            Currency = "eur",
            UnitAmount = unitAmount,
            Recurring = new PriceRecurringOptions
            {
                Interval = plan.BillingInterval == BillingInterval.Monthly ? "month" : "year"
            },
            Metadata = new Dictionary<string, string>
            {
                ["planKey"] = plan.PlanKey,
                ["planVersion"] = plan.Version.ToString(),
                ["billingInterval"] = plan.BillingInterval.ToString(),
                ["family"] = plan.Family.ToString()
            }
        }).GetAwaiter().GetResult();

        return price.Id;
    }
}
