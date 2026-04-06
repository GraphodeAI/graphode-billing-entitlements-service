using System.Collections.Concurrent;
using Graphode.BillingEntitlementsService.Contracts.Billing;
using Graphode.BillingEntitlementsService.Domain.Billing;
using Microsoft.Extensions.DependencyInjection;

namespace Graphode.BillingEntitlementsService.Application.Services;

public interface IBillingCatalogStore
{
    IReadOnlyList<PlanDefinition> ListPlans();
    WorkspaceBillingViewResponse GetWorkspaceBillingView(string workspaceId);
    WorkspaceBillingViewResponse StartSubscription(string workspaceId, StartSubscriptionCommandRequest request);
    WorkspaceBillingViewResponse ChangeSubscription(string workspaceId, ChangeSubscriptionCommandRequest request);
    WorkspaceBillingViewResponse CancelSubscription(string workspaceId, CancelSubscriptionCommandRequest request);
}

public sealed class BillingCatalogStore : IBillingCatalogStore
{
    private readonly ConcurrentDictionary<string, BillingAccount> _accounts = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, Subscription> _subscriptions = new(StringComparer.OrdinalIgnoreCase);
    private readonly IReadOnlyList<PlanDefinition> _plans = BillingSeed.Plans;

    public IReadOnlyList<PlanDefinition> ListPlans() => _plans;

    public WorkspaceBillingViewResponse GetWorkspaceBillingView(string workspaceId)
    {
        var account = GetOrCreateAccount(workspaceId);
        var subscription = GetSubscription(account.CurrentSubscriptionId);

        return ToResponse(account, subscription);
    }

    public WorkspaceBillingViewResponse StartSubscription(string workspaceId, StartSubscriptionCommandRequest request)
    {
        var account = GetOrCreateAccount(workspaceId, request.OwnerScopeType);
        var plan = ResolvePlan(request.PlanKey, request.BillingInterval, account.OwnerScopeType);
        var now = DateTimeOffset.UtcNow;
        var subscriptionId = account.CurrentSubscriptionId ?? $"sub_{account.BillingAccountId}";
        var subscription = new Subscription(
            subscriptionId,
            account.BillingAccountId,
            $"sub_stripe_{account.BillingAccountId}",
            SubscriptionStatus.Active,
            plan.PlanKey,
            plan.Version,
            plan.BillingInterval,
            now,
            plan.BillingInterval == BillingInterval.Monthly ? now.AddMonths(1) : now.AddYears(1),
            cancelAtPeriodEnd: false);

        _subscriptions[subscription.SubscriptionId] = subscription;
        account.AttachSubscription(plan, subscription);

        return ToResponse(account, subscription);
    }

    public WorkspaceBillingViewResponse ChangeSubscription(string workspaceId, ChangeSubscriptionCommandRequest request)
    {
        var account = GetOrCreateAccount(workspaceId);
        var existing = GetSubscription(account.CurrentSubscriptionId) ?? throw new InvalidOperationException($"Workspace {workspaceId} has no subscription.");
        var plan = ResolvePlan(request.PlanKey, request.BillingInterval, account.OwnerScopeType);
        existing.ChangePlan(plan, DateTimeOffset.UtcNow);
        account.AttachSubscription(plan, existing);

        return ToResponse(account, existing);
    }

    public WorkspaceBillingViewResponse CancelSubscription(string workspaceId, CancelSubscriptionCommandRequest request)
    {
        _ = request;
        var account = GetOrCreateAccount(workspaceId);
        var existing = GetSubscription(account.CurrentSubscriptionId) ?? throw new InvalidOperationException($"Workspace {workspaceId} has no subscription.");
        existing.Cancel(DateTimeOffset.UtcNow);
        account.Cancel();

        return ToResponse(account, existing);
    }

    private BillingAccount GetOrCreateAccount(string workspaceId, BillingAccountOwnerScopeType? scopeType = null)
    {
        return _accounts.GetOrAdd(workspaceId, key =>
        {
            var ownerScopeType = scopeType ?? InferScopeType(key);
            var plan = BillingSeed.DefaultPlanFor(ownerScopeType);
            var now = DateTimeOffset.UtcNow;
            var account = new BillingAccount(
                billingAccountId: $"ba_{key}",
                workspaceId: key,
                ownerScopeType: ownerScopeType,
                ownerScopeId: key,
                status: BillingAccountStatus.Active,
                currency: "EUR",
                currentPlanKey: plan.PlanKey,
                currentPlanVersion: plan.Version,
                currentSubscriptionId: null,
                sharedPoolMode: ownerScopeType == BillingAccountOwnerScopeType.OrganizationWorkspace
                    ? BillingSharedPoolMode.WorkspaceSharedPool
                    : BillingSharedPoolMode.None,
                creditBalance: plan.IncludedCredits,
                reservedCreditBalance: 0,
                allocationPolicyId: ownerScopeType == BillingAccountOwnerScopeType.OrganizationWorkspace ? $"cap_{key}" : null,
                createdAtUtc: now);

            var subscription = new Subscription(
                subscriptionId: $"sub_{key}",
                billingAccountId: account.BillingAccountId,
                providerSubscriptionId: $"sub_stripe_{key}",
                status: SubscriptionStatus.Active,
                planKey: plan.PlanKey,
                planVersion: plan.Version,
                billingInterval: plan.BillingInterval,
                currentPeriodStartUtc: now,
                currentPeriodEndUtc: now.AddMonths(1),
                cancelAtPeriodEnd: false);

            _subscriptions[subscription.SubscriptionId] = subscription;
            account.AttachSubscription(plan, subscription);
            return account;
        });
    }

    private Subscription? GetSubscription(string? subscriptionId) =>
        subscriptionId is null ? null : _subscriptions.TryGetValue(subscriptionId, out var subscription) ? subscription : null;

    private PlanDefinition ResolvePlan(string planKey, BillingInterval billingInterval, BillingAccountOwnerScopeType ownerScopeType)
    {
        var plan = _plans.FirstOrDefault(item =>
            string.Equals(item.PlanKey, planKey, StringComparison.OrdinalIgnoreCase) &&
            item.BillingInterval == billingInterval);

        if (plan is null)
        {
            throw new InvalidOperationException($"Unknown billing plan {planKey} for interval {billingInterval}.");
        }

        var expectedFamily = ownerScopeType == BillingAccountOwnerScopeType.OrganizationWorkspace
            ? BillingPlanFamily.Organization
            : BillingPlanFamily.Solo;

        if (plan.Family != expectedFamily)
        {
            throw new InvalidOperationException($"Plan {planKey} is not available for {ownerScopeType}.");
        }

        return plan;
    }

    private static BillingAccountOwnerScopeType InferScopeType(string workspaceId)
    {
        return workspaceId.Contains("org", StringComparison.OrdinalIgnoreCase) ||
               workspaceId.Contains("team", StringComparison.OrdinalIgnoreCase)
            ? BillingAccountOwnerScopeType.OrganizationWorkspace
            : BillingAccountOwnerScopeType.PersonalWorkspace;
    }

    private static WorkspaceBillingViewResponse ToResponse(BillingAccount account, Subscription? subscription)
    {
        var plans = BillingSeed.Plans.Select(MapPlan).ToArray();
        return new WorkspaceBillingViewResponse(MapAccount(account), subscription is null ? null : MapSubscription(subscription), new BillingPlansResponse(plans));
    }

    private static BillingPlanResponse MapPlan(PlanDefinition plan) =>
        new(plan.PlanKey, plan.Version, plan.Family, plan.BillingInterval, plan.StripePriceId, plan.IncludedCredits, plan.Entitlements, plan.DefaultBudgetBehavior, plan.Recommended);

    private static BillingAccountResponse MapAccount(BillingAccount account) =>
        new(account.BillingAccountId, account.WorkspaceId, account.OwnerScopeType, account.OwnerScopeId, account.Status, account.Currency, account.CurrentPlanKey, account.CurrentPlanVersion, account.CurrentSubscriptionId, account.SharedPoolMode, account.CreditBalance, account.ReservedCreditBalance, account.AllocationPolicyId, account.CreatedAtUtc);

    private static SubscriptionResponse MapSubscription(Subscription subscription) =>
        new(subscription.SubscriptionId, subscription.BillingAccountId, subscription.Provider, subscription.ProviderSubscriptionId, subscription.Status, subscription.PlanKey, subscription.PlanVersion, subscription.BillingInterval, subscription.CurrentPeriodStartUtc, subscription.CurrentPeriodEndUtc, subscription.CancelAtPeriodEnd);
}

public sealed class BillingWorkspaceService
{
    private readonly IBillingCatalogStore _store;

    public BillingWorkspaceService(IBillingCatalogStore store)
    {
        _store = store;
    }

    public Task<IReadOnlyList<BillingPlanResponse>> ListPlansAsync(CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        return Task.FromResult<IReadOnlyList<BillingPlanResponse>>(
            _store.ListPlans().Select(plan => new BillingPlanResponse(plan.PlanKey, plan.Version, plan.Family, plan.BillingInterval, plan.StripePriceId, plan.IncludedCredits, plan.Entitlements, plan.DefaultBudgetBehavior, plan.Recommended)).ToArray());
    }

    public Task<WorkspaceBillingViewResponse> GetWorkspaceBillingViewAsync(string workspaceId, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        return Task.FromResult(_store.GetWorkspaceBillingView(workspaceId));
    }

    public Task<WorkspaceBillingViewResponse> StartSubscriptionAsync(string workspaceId, StartSubscriptionCommandRequest request, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        return Task.FromResult(_store.StartSubscription(workspaceId, request));
    }

    public Task<WorkspaceBillingViewResponse> ChangeSubscriptionAsync(string workspaceId, ChangeSubscriptionCommandRequest request, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        return Task.FromResult(_store.ChangeSubscription(workspaceId, request));
    }

    public Task<WorkspaceBillingViewResponse> CancelSubscriptionAsync(string workspaceId, CancelSubscriptionCommandRequest request, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        return Task.FromResult(_store.CancelSubscription(workspaceId, request));
    }
}
