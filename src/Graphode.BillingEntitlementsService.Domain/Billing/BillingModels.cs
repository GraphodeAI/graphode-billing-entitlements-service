namespace Graphode.BillingEntitlementsService.Domain.Billing;

public enum BillingAccountOwnerScopeType
{
    PersonalWorkspace = 0,
    OrganizationWorkspace = 1
}

public enum BillingAccountStatus
{
    PendingSetup = 0,
    Active = 1,
    PastDue = 2,
    Suspended = 3,
    Cancelled = 4
}

public enum BillingPlanFamily
{
    Solo = 0,
    Organization = 1
}

public enum BillingInterval
{
    Monthly = 0,
    Yearly = 1
}

public enum SubscriptionStatus
{
    Incomplete = 0,
    Trialing = 1,
    Active = 2,
    PastDue = 3,
    Cancelled = 4
}

public enum BillingSharedPoolMode
{
    None = 0,
    WorkspaceSharedPool = 1
}

public enum PaymentMethodRefStatus
{
    Active = 0,
    Detached = 1,
    Expired = 2
}

public enum BudgetBehavior
{
    PauseOnExhaust = 0,
    AllowOverage = 1
}

public enum LedgerEntryType
{
    CreditGrant = 0,
    UsageReserve = 1,
    UsageCommit = 2,
    UsageRelease = 3,
    Adjustment = 4
}

public sealed record BillingEntitlements(
    int MaxProjects,
    int MaxMembers,
    bool AiValidation,
    bool Simulation,
    bool ExternalMcpUsage);

public sealed record PlanDefinition(
    string PlanKey,
    int Version,
    BillingPlanFamily Family,
    BillingInterval BillingInterval,
    string StripePriceId,
    decimal IncludedCredits,
    BillingEntitlements Entitlements,
    BudgetBehavior DefaultBudgetBehavior,
    bool Recommended);

public sealed record WalletBalance(
    decimal AvailableCredits,
    decimal ReservedCredits,
    string Currency,
    DateTimeOffset UpdatedAtUtc);

public sealed record LedgerEntry(
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

public sealed record PaymentMethodRef(
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

public sealed record BudgetPolicy(
    string ScopeType,
    string ScopeRefId,
    string Category,
    string Period,
    decimal LimitAmount,
    string EnforcementMode,
    DateTimeOffset CreatedAtUtc);

public sealed class BillingAccount
{
    public BillingAccount(
        string billingAccountId,
        string workspaceId,
        BillingAccountOwnerScopeType ownerScopeType,
        string ownerScopeId,
        BillingAccountStatus status,
        string currency,
        string currentPlanKey,
        int currentPlanVersion,
        string? currentSubscriptionId,
        BillingSharedPoolMode sharedPoolMode,
        decimal creditBalance,
        decimal reservedCreditBalance,
        string? defaultPaymentMethodRefId,
        string? allocationPolicyId,
        DateTimeOffset createdAtUtc)
    {
        BillingAccountId = billingAccountId;
        WorkspaceId = workspaceId;
        OwnerScopeType = ownerScopeType;
        OwnerScopeId = ownerScopeId;
        Status = status;
        Currency = currency;
        CurrentPlanKey = currentPlanKey;
        CurrentPlanVersion = currentPlanVersion;
        CurrentSubscriptionId = currentSubscriptionId;
        SharedPoolMode = sharedPoolMode;
        CreditBalance = creditBalance;
        ReservedCreditBalance = reservedCreditBalance;
        DefaultPaymentMethodRefId = defaultPaymentMethodRefId;
        AllocationPolicyId = allocationPolicyId;
        CreatedAtUtc = createdAtUtc;
    }

    public string BillingAccountId { get; }

    public string WorkspaceId { get; }

    public BillingAccountOwnerScopeType OwnerScopeType { get; }

    public string OwnerScopeId { get; }

    public BillingAccountStatus Status { get; private set; }

    public string Currency { get; }

    public string CurrentPlanKey { get; private set; }

    public int CurrentPlanVersion { get; private set; }

    public string? CurrentSubscriptionId { get; private set; }

    public BillingSharedPoolMode SharedPoolMode { get; private set; }

    public decimal CreditBalance { get; private set; }

    public decimal ReservedCreditBalance { get; private set; }

    public string? DefaultPaymentMethodRefId { get; private set; }

    public string? AllocationPolicyId { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; }

    public void AttachSubscription(PlanDefinition plan, Subscription subscription)
    {
        CurrentPlanKey = plan.PlanKey;
        CurrentPlanVersion = plan.Version;
        CurrentSubscriptionId = subscription.SubscriptionId;
        Status = BillingAccountStatus.Active;
        SharedPoolMode = plan.Family == BillingPlanFamily.Organization
            ? BillingSharedPoolMode.WorkspaceSharedPool
            : BillingSharedPoolMode.None;
        CreditBalance = Math.Max(CreditBalance, plan.IncludedCredits);
    }

    public void Cancel()
    {
        Status = BillingAccountStatus.Cancelled;
    }

    public void SyncWallet(WalletBalance walletBalance)
    {
        CreditBalance = walletBalance.AvailableCredits;
        ReservedCreditBalance = walletBalance.ReservedCredits;
    }

    public void SetDefaultPaymentMethod(string? paymentMethodRefId)
    {
        DefaultPaymentMethodRefId = paymentMethodRefId;
    }

    public void ReserveCredits(decimal amount)
    {
        CreditBalance -= amount;
        ReservedCreditBalance += amount;
    }

    public void CommitCredits(decimal amount)
    {
        ReservedCreditBalance -= amount;
    }

    public void ReleaseCredits(decimal amount)
    {
        CreditBalance += amount;
        ReservedCreditBalance -= amount;
    }
}

public sealed class Subscription
{
    public Subscription(
        string subscriptionId,
        string billingAccountId,
        string providerSubscriptionId,
        SubscriptionStatus status,
        string planKey,
        int planVersion,
        BillingInterval billingInterval,
        DateTimeOffset currentPeriodStartUtc,
        DateTimeOffset currentPeriodEndUtc,
        bool cancelAtPeriodEnd)
    {
        SubscriptionId = subscriptionId;
        BillingAccountId = billingAccountId;
        Provider = "STRIPE";
        ProviderSubscriptionId = providerSubscriptionId;
        Status = status;
        PlanKey = planKey;
        PlanVersion = planVersion;
        BillingInterval = billingInterval;
        CurrentPeriodStartUtc = currentPeriodStartUtc;
        CurrentPeriodEndUtc = currentPeriodEndUtc;
        CancelAtPeriodEnd = cancelAtPeriodEnd;
    }

    public string SubscriptionId { get; }

    public string BillingAccountId { get; }

    public string Provider { get; }

    public string ProviderSubscriptionId { get; private set; }

    public SubscriptionStatus Status { get; private set; }

    public string PlanKey { get; private set; }

    public int PlanVersion { get; private set; }

    public BillingInterval BillingInterval { get; private set; }

    public DateTimeOffset CurrentPeriodStartUtc { get; private set; }

    public DateTimeOffset CurrentPeriodEndUtc { get; private set; }

    public bool CancelAtPeriodEnd { get; private set; }

    public void ChangePlan(PlanDefinition plan, DateTimeOffset timestampUtc)
    {
        PlanKey = plan.PlanKey;
        PlanVersion = plan.Version;
        BillingInterval = plan.BillingInterval;
        Status = SubscriptionStatus.Active;
        CurrentPeriodStartUtc = timestampUtc;
        CurrentPeriodEndUtc = plan.BillingInterval == BillingInterval.Monthly
            ? timestampUtc.AddMonths(1)
            : timestampUtc.AddYears(1);
        CancelAtPeriodEnd = false;
    }

    public void Cancel(DateTimeOffset timestampUtc)
    {
        Status = SubscriptionStatus.Cancelled;
        CancelAtPeriodEnd = true;
        CurrentPeriodEndUtc = timestampUtc;
    }
}

public static class BillingSeed
{
    public static IReadOnlyList<PlanDefinition> Plans { get; } = new[]
    {
        new PlanDefinition(
            "SOLO_STARTER",
            1,
            BillingPlanFamily.Solo,
            BillingInterval.Monthly,
            "price_solo_starter_monthly",
            2500,
            new BillingEntitlements(10, 3, false, false, false),
            BudgetBehavior.PauseOnExhaust,
            true),
        new PlanDefinition(
            "ORG_PRO",
            1,
            BillingPlanFamily.Organization,
            BillingInterval.Monthly,
            "price_org_pro_monthly",
            10000,
            new BillingEntitlements(50, 100, true, true, true),
            BudgetBehavior.AllowOverage,
            true)
    };

    public static PlanDefinition DefaultPlanFor(BillingAccountOwnerScopeType scopeType) =>
        Plans.First(plan =>
            scopeType == BillingAccountOwnerScopeType.OrganizationWorkspace
                ? plan.Family == BillingPlanFamily.Organization
                : plan.Family == BillingPlanFamily.Solo);
}
