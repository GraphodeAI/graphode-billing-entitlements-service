using Graphode.BillingEntitlementsService.Domain.Billing;

namespace Graphode.BillingEntitlementsService.Contracts.Billing;

public sealed record BillingPlansResponse(IReadOnlyList<BillingPlanResponse> Plans);

public sealed record BillingPlanResponse(
    string PlanKey,
    int Version,
    BillingPlanFamily Family,
    BillingInterval BillingInterval,
    string StripePriceId,
    decimal BasePrice,
    decimal? SeatPrice,
    decimal IncludedCredits,
    BillingEntitlements Entitlements,
    BudgetBehavior DefaultBudgetBehavior,
    bool Recommended);

public sealed record BillingAccountResponse(
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
    DateTimeOffset CreatedAtUtc);

public sealed record SubscriptionResponse(
    string SubscriptionId,
    string BillingAccountId,
    string Provider,
    string ProviderSubscriptionId,
    string? StripeSubscriptionItemId,
    SubscriptionStatus Status,
    string PlanKey,
    int PlanVersion,
    BillingInterval BillingInterval,
    DateTimeOffset CurrentPeriodStartUtc,
    DateTimeOffset CurrentPeriodEndUtc,
    bool CancelAtPeriodEnd);

public sealed record WorkspaceBillingViewResponse(
    BillingAccountResponse Account,
    SubscriptionResponse? Subscription,
    BillingPlansResponse Plans);

public sealed record PaymentMethodRefResponse(
    string PaymentMethodRefId,
    string BillingAccountId,
    string StripePaymentMethodId,
    string Type,
    string? Brand,
    string? Last4,
    int? ExpMonth,
    int? ExpYear,
    bool IsDefault,
    PaymentMethodRefStatus Status,
    DateTimeOffset CreatedAtUtc);

public sealed record WorkspacePaymentMethodsResponse(
    BillingAccountResponse Account,
    IReadOnlyList<PaymentMethodRefResponse> PaymentMethods,
    string? SetupIntentId = null,
    string? SetupIntentClientSecret = null,
    string? StripeCustomerId = null);

public sealed record WorkspaceLedgerViewResponse(
    BillingAccountResponse Account,
    WalletBalanceResponse Wallet,
    BudgetPolicyResponse Policy,
    IReadOnlyList<LedgerEntryResponse> LedgerEntries);

public sealed record StartSubscriptionCommandRequest(
    string PlanKey,
    BillingInterval BillingInterval,
    string? Currency,
    BillingAccountOwnerScopeType? OwnerScopeType);

public sealed record ChangeSubscriptionCommandRequest(
    string PlanKey,
    BillingInterval BillingInterval);

public sealed record CancelSubscriptionCommandRequest(
    string? Reason);

public sealed record SetupPaymentMethodCommandRequest(
    string Type,
    string? Brand,
    string? Last4,
    int? ExpMonth,
    int? ExpYear,
    bool IsDefault,
    string? SetupIntentId = null,
    string? StripePaymentMethodId = null,
    string? StripeCustomerId = null);

public sealed record ReserveCreditsCommandRequest(
    decimal Amount,
    string ReferenceId,
    string? ProjectId,
    string? UserId,
    string? AllocationId);

public sealed record CommitCreditsCommandRequest(
    decimal Amount,
    string ReferenceId,
    string? ProjectId,
    string? UserId,
    string? AllocationId);

public sealed record ReleaseCreditsCommandRequest(
    decimal Amount,
    string ReferenceId,
    string? ProjectId,
    string? UserId,
    string? AllocationId);

public sealed record WalletBalanceResponse(
    decimal AvailableCredits,
    decimal ReservedCredits,
    string Currency,
    DateTimeOffset UpdatedAtUtc);

public sealed record BudgetPolicyResponse(
    string ScopeType,
    string ScopeRefId,
    string Category,
    string Period,
    decimal LimitAmount,
    string EnforcementMode,
    DateTimeOffset CreatedAtUtc);

public sealed record LedgerEntryResponse(
    string LedgerEntryId,
    string BillingAccountId,
    LedgerEntryType Type,
    decimal Amount,
    string Currency,
    string ReferenceId,
    string? ProjectId,
    string? UserId,
    string? AllocationId,
    DateTimeOffset CreatedAtUtc);

public sealed record StripeWebhookProcessingResponse(
    string EventId,
    string EventType,
    string ProcessingStatus,
    string? BillingAccountId,
    string? SubscriptionId,
    string? Details);
