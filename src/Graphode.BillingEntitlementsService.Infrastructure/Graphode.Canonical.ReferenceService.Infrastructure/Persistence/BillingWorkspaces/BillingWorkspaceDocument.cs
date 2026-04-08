using Graphode.BillingEntitlementsService.Application.Abstractions.Persistence;
using Graphode.BillingEntitlementsService.Domain.Billing;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Graphode.BillingEntitlementsService.Infrastructure.Persistence.BillingWorkspaces;

[BsonIgnoreExtraElements]
public sealed class BillingWorkspaceDocument : IMongoDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public string Id { get; init; } = string.Empty;

    [BsonElement("workspaceId")]
    public string WorkspaceId { get; init; } = string.Empty;

    [BsonElement("ownerScopeType")]
    public BillingAccountOwnerScopeType OwnerScopeType { get; init; }

    [BsonElement("ownerScopeId")]
    public string OwnerScopeId { get; init; } = string.Empty;

    [BsonElement("status")]
    public BillingAccountStatus Status { get; init; }

    [BsonElement("currency")]
    public string Currency { get; init; } = "EUR";

    [BsonElement("currentPlanKey")]
    public string CurrentPlanKey { get; init; } = string.Empty;

    [BsonElement("currentPlanVersion")]
    public int CurrentPlanVersion { get; init; }

    [BsonElement("currentSubscriptionId")]
    [BsonIgnoreIfNull]
    public string? CurrentSubscriptionId { get; init; }

    [BsonElement("stripeCustomerId")]
    [BsonIgnoreIfNull]
    public string? StripeCustomerId { get; init; }

    [BsonElement("sharedPoolMode")]
    public BillingSharedPoolMode SharedPoolMode { get; init; }

    [BsonElement("creditBalance")]
    public decimal CreditBalance { get; init; }

    [BsonElement("reservedCreditBalance")]
    public decimal ReservedCreditBalance { get; init; }

    [BsonElement("defaultPaymentMethodRefId")]
    [BsonIgnoreIfNull]
    public string? DefaultPaymentMethodRefId { get; init; }

    [BsonElement("allocationPolicyId")]
    [BsonIgnoreIfNull]
    public string? AllocationPolicyId { get; init; }

    [BsonElement("createdAtUtc")]
    public DateTimeOffset CreatedAtUtc { get; init; }

    [BsonElement("subscription")]
    [BsonIgnoreIfNull]
    public BillingSubscriptionDocument? Subscription { get; init; }

    [BsonElement("paymentMethods")]
    public IReadOnlyList<BillingPaymentMethodDocument> PaymentMethods { get; init; } = [];

    [BsonElement("ledgerEntries")]
    public IReadOnlyList<BillingLedgerEntryDocument> LedgerEntries { get; init; } = [];

    [BsonElement("processedStripeEventIds")]
    public IReadOnlyList<string> ProcessedStripeEventIds { get; init; } = [];

    public static BillingWorkspaceDocument FromSnapshot(BillingWorkspaceSnapshot snapshot) =>
        new()
        {
            Id = snapshot.WorkspaceId,
            WorkspaceId = snapshot.WorkspaceId,
            OwnerScopeType = snapshot.OwnerScopeType,
            OwnerScopeId = snapshot.OwnerScopeId,
            Status = snapshot.Status,
            Currency = snapshot.Currency,
            CurrentPlanKey = snapshot.CurrentPlanKey,
            CurrentPlanVersion = snapshot.CurrentPlanVersion,
            CurrentSubscriptionId = snapshot.CurrentSubscriptionId,
            StripeCustomerId = snapshot.StripeCustomerId,
            SharedPoolMode = snapshot.SharedPoolMode,
            CreditBalance = snapshot.CreditBalance,
            ReservedCreditBalance = snapshot.ReservedCreditBalance,
            DefaultPaymentMethodRefId = snapshot.DefaultPaymentMethodRefId,
            AllocationPolicyId = snapshot.AllocationPolicyId,
            CreatedAtUtc = snapshot.CreatedAtUtc,
            Subscription = snapshot.Subscription is null ? null : BillingSubscriptionDocument.FromSnapshot(snapshot.Subscription),
            PaymentMethods = snapshot.PaymentMethods.Select(BillingPaymentMethodDocument.FromDomain).ToArray(),
            LedgerEntries = snapshot.LedgerEntries.Select(BillingLedgerEntryDocument.FromDomain).ToArray(),
            ProcessedStripeEventIds = snapshot.ProcessedStripeEventIds.ToArray()
        };

    public BillingWorkspaceSnapshot ToSnapshot() =>
        new(
            BillingAccountId: Id,
            WorkspaceId: WorkspaceId,
            OwnerScopeType: OwnerScopeType,
            OwnerScopeId: OwnerScopeId,
            Status: Status,
            Currency: Currency,
            CurrentPlanKey: CurrentPlanKey,
            CurrentPlanVersion: CurrentPlanVersion,
            CurrentSubscriptionId: CurrentSubscriptionId,
            StripeCustomerId: StripeCustomerId,
            SharedPoolMode: SharedPoolMode,
            CreditBalance: CreditBalance,
            ReservedCreditBalance: ReservedCreditBalance,
            DefaultPaymentMethodRefId: DefaultPaymentMethodRefId,
            AllocationPolicyId: AllocationPolicyId,
            CreatedAtUtc: CreatedAtUtc,
            Subscription: Subscription?.ToSnapshot(),
            PaymentMethods: PaymentMethods.Select(paymentMethod => paymentMethod.ToDomain()).ToArray(),
            LedgerEntries: LedgerEntries.Select(ledgerEntry => ledgerEntry.ToDomain()).ToArray(),
            ProcessedStripeEventIds: ProcessedStripeEventIds.ToArray());
}

[BsonIgnoreExtraElements]
public sealed class BillingSubscriptionDocument
{
    [BsonElement("subscriptionId")]
    public string SubscriptionId { get; init; } = string.Empty;

    [BsonElement("billingAccountId")]
    public string BillingAccountId { get; init; } = string.Empty;

    [BsonElement("providerSubscriptionId")]
    public string ProviderSubscriptionId { get; init; } = string.Empty;

    [BsonElement("stripeSubscriptionItemId")]
    [BsonIgnoreIfNull]
    public string? StripeSubscriptionItemId { get; init; }

    [BsonElement("status")]
    public SubscriptionStatus Status { get; init; }

    [BsonElement("planKey")]
    public string PlanKey { get; init; } = string.Empty;

    [BsonElement("planVersion")]
    public int PlanVersion { get; init; }

    [BsonElement("billingInterval")]
    public BillingInterval BillingInterval { get; init; }

    [BsonElement("currentPeriodStartUtc")]
    public DateTimeOffset CurrentPeriodStartUtc { get; init; }

    [BsonElement("currentPeriodEndUtc")]
    public DateTimeOffset CurrentPeriodEndUtc { get; init; }

    [BsonElement("cancelAtPeriodEnd")]
    public bool CancelAtPeriodEnd { get; init; }

    public static BillingSubscriptionDocument FromSnapshot(BillingSubscriptionSnapshot snapshot) =>
        new()
        {
            SubscriptionId = snapshot.SubscriptionId,
            BillingAccountId = snapshot.BillingAccountId,
            ProviderSubscriptionId = snapshot.ProviderSubscriptionId,
            StripeSubscriptionItemId = snapshot.StripeSubscriptionItemId,
            Status = snapshot.Status,
            PlanKey = snapshot.PlanKey,
            PlanVersion = snapshot.PlanVersion,
            BillingInterval = snapshot.BillingInterval,
            CurrentPeriodStartUtc = snapshot.CurrentPeriodStartUtc,
            CurrentPeriodEndUtc = snapshot.CurrentPeriodEndUtc,
            CancelAtPeriodEnd = snapshot.CancelAtPeriodEnd
        };

    public BillingSubscriptionSnapshot ToSnapshot() =>
        new(
            SubscriptionId,
            BillingAccountId,
            ProviderSubscriptionId,
            StripeSubscriptionItemId,
            Status,
            PlanKey,
            PlanVersion,
            BillingInterval,
            CurrentPeriodStartUtc,
            CurrentPeriodEndUtc,
            CancelAtPeriodEnd);
}

[BsonIgnoreExtraElements]
public sealed class BillingPaymentMethodDocument
{
    [BsonElement("paymentMethodRefId")]
    public string PaymentMethodRefId { get; init; } = string.Empty;

    [BsonElement("billingAccountId")]
    public string BillingAccountId { get; init; } = string.Empty;

    [BsonElement("stripePaymentMethodId")]
    public string StripePaymentMethodId { get; init; } = string.Empty;

    [BsonElement("type")]
    public string Type { get; init; } = string.Empty;

    [BsonElement("brand")]
    [BsonIgnoreIfNull]
    public string? Brand { get; init; }

    [BsonElement("last4")]
    [BsonIgnoreIfNull]
    public string? Last4 { get; init; }

    [BsonElement("expMonth")]
    [BsonIgnoreIfNull]
    public int? ExpMonth { get; init; }

    [BsonElement("expYear")]
    [BsonIgnoreIfNull]
    public int? ExpYear { get; init; }

    [BsonElement("isDefault")]
    public bool IsDefault { get; init; }

    [BsonElement("status")]
    public PaymentMethodRefStatus Status { get; init; }

    [BsonElement("createdAtUtc")]
    public DateTimeOffset CreatedAtUtc { get; init; }

    public static BillingPaymentMethodDocument FromDomain(PaymentMethodRef paymentMethod) =>
        new()
        {
            PaymentMethodRefId = paymentMethod.PaymentMethodRefId,
            BillingAccountId = paymentMethod.BillingAccountId,
            StripePaymentMethodId = paymentMethod.StripePaymentMethodId,
            Type = paymentMethod.Type,
            Brand = paymentMethod.Brand,
            Last4 = paymentMethod.Last4,
            ExpMonth = paymentMethod.ExpMonth,
            ExpYear = paymentMethod.ExpYear,
            IsDefault = paymentMethod.IsDefault,
            Status = paymentMethod.Status,
            CreatedAtUtc = paymentMethod.CreatedAtUtc
        };

    public PaymentMethodRef ToDomain() =>
        new(
            PaymentMethodRefId,
            BillingAccountId,
            StripePaymentMethodId,
            Type,
            Brand,
            Last4,
            ExpMonth,
            ExpYear,
            IsDefault,
            Status,
            CreatedAtUtc);
}

[BsonIgnoreExtraElements]
public sealed class BillingLedgerEntryDocument
{
    [BsonElement("ledgerEntryId")]
    public string LedgerEntryId { get; init; } = string.Empty;

    [BsonElement("billingAccountId")]
    public string BillingAccountId { get; init; } = string.Empty;

    [BsonElement("type")]
    public LedgerEntryType Type { get; init; }

    [BsonElement("amount")]
    public decimal Amount { get; init; }

    [BsonElement("currency")]
    public string Currency { get; init; } = string.Empty;

    [BsonElement("referenceId")]
    public string ReferenceId { get; init; } = string.Empty;

    [BsonElement("projectId")]
    [BsonIgnoreIfNull]
    public string? ProjectId { get; init; }

    [BsonElement("userId")]
    [BsonIgnoreIfNull]
    public string? UserId { get; init; }

    [BsonElement("allocationId")]
    [BsonIgnoreIfNull]
    public string? AllocationId { get; init; }

    [BsonElement("createdAtUtc")]
    public DateTimeOffset CreatedAtUtc { get; init; }

    public static BillingLedgerEntryDocument FromDomain(LedgerEntry ledgerEntry) =>
        new()
        {
            LedgerEntryId = ledgerEntry.LedgerEntryId,
            BillingAccountId = ledgerEntry.BillingAccountId,
            Type = ledgerEntry.Type,
            Amount = ledgerEntry.Amount,
            Currency = ledgerEntry.Currency,
            ReferenceId = ledgerEntry.ReferenceId,
            ProjectId = ledgerEntry.ProjectId,
            UserId = ledgerEntry.UserId,
            AllocationId = ledgerEntry.AllocationId,
            CreatedAtUtc = ledgerEntry.CreatedAtUtc
        };

    public LedgerEntry ToDomain() =>
        new(
            LedgerEntryId,
            BillingAccountId,
            Type,
            Amount,
            Currency,
            ReferenceId,
            ProjectId,
            UserId,
            AllocationId,
            CreatedAtUtc);
}
