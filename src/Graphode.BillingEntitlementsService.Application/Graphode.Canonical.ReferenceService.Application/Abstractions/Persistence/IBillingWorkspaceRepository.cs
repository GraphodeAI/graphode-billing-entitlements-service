using Graphode.BillingEntitlementsService.Domain.Billing;

namespace Graphode.BillingEntitlementsService.Application.Abstractions.Persistence;

public sealed record BillingSubscriptionSnapshot(
    string SubscriptionId,
    string BillingAccountId,
    string ProviderSubscriptionId,
    string? StripeSubscriptionItemId,
    SubscriptionStatus Status,
    string PlanKey,
    int PlanVersion,
    BillingInterval BillingInterval,
    DateTimeOffset CurrentPeriodStartUtc,
    DateTimeOffset CurrentPeriodEndUtc,
    bool CancelAtPeriodEnd);

public sealed record BillingWorkspaceSnapshot(
    string BillingAccountId,
    string WorkspaceId,
    BillingAccountOwnerScopeType OwnerScopeType,
    string OwnerScopeId,
    BillingAccountStatus Status,
    string Currency,
    string CurrentPlanKey,
    int CurrentPlanVersion,
    string? CurrentSubscriptionId,
    string? StripeCustomerId,
    BillingSharedPoolMode SharedPoolMode,
    decimal CreditBalance,
    decimal ReservedCreditBalance,
    string? DefaultPaymentMethodRefId,
    string? AllocationPolicyId,
    DateTimeOffset CreatedAtUtc,
    BillingSubscriptionSnapshot? Subscription,
    IReadOnlyList<PaymentMethodRef> PaymentMethods,
    IReadOnlyList<LedgerEntry> LedgerEntries,
    IReadOnlyList<string> ProcessedStripeEventIds);

public interface IBillingWorkspaceRepository
{
    BillingWorkspaceSnapshot? GetByWorkspaceId(string workspaceId);

    BillingWorkspaceSnapshot? FindByStripeCustomerId(string stripeCustomerId);

    BillingWorkspaceSnapshot? FindByProviderSubscriptionId(string providerSubscriptionId);

    void Upsert(BillingWorkspaceSnapshot snapshot);
}
