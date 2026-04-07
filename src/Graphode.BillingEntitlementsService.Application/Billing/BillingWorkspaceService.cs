using System.Collections.Concurrent;
using Graphode.BillingEntitlementsService.Contracts.Billing;
using Graphode.BillingEntitlementsService.Domain.Billing;
using Microsoft.Extensions.DependencyInjection;

namespace Graphode.BillingEntitlementsService.Application.Services;

public interface IBillingCatalogStore
{
    IReadOnlyList<PlanDefinition> ListPlans();
    WorkspaceBillingViewResponse GetWorkspaceBillingView(string workspaceId);
    WorkspaceLedgerViewResponse GetWorkspaceLedgerView(string workspaceId);
    WorkspaceBillingViewResponse StartSubscription(string workspaceId, StartSubscriptionCommandRequest request);
    WorkspaceBillingViewResponse ChangeSubscription(string workspaceId, ChangeSubscriptionCommandRequest request);
    WorkspaceBillingViewResponse CancelSubscription(string workspaceId, CancelSubscriptionCommandRequest request);
    WorkspaceLedgerViewResponse ReserveCredits(string workspaceId, ReserveCreditsCommandRequest request);
    WorkspaceLedgerViewResponse CommitCredits(string workspaceId, CommitCreditsCommandRequest request);
    WorkspaceLedgerViewResponse ReleaseCredits(string workspaceId, ReleaseCreditsCommandRequest request);
}

public sealed class BillingCatalogStore : IBillingCatalogStore
{
    private readonly ConcurrentDictionary<string, BillingAccount> _accounts = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, Subscription> _subscriptions = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, WalletBalance> _walletBalances = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, BudgetPolicy> _budgetPolicies = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, ConcurrentQueue<LedgerEntry>> _ledgerEntries = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, object> _accountLocks = new(StringComparer.OrdinalIgnoreCase);
    private readonly IReadOnlyList<PlanDefinition> _plans = BillingSeed.Plans;

    public IReadOnlyList<PlanDefinition> ListPlans() => _plans;

    public WorkspaceBillingViewResponse GetWorkspaceBillingView(string workspaceId)
    {
        var account = GetOrCreateAccount(workspaceId);
        var subscription = GetSubscription(account.CurrentSubscriptionId);

        return ToResponse(account, subscription);
    }

    public WorkspaceLedgerViewResponse GetWorkspaceLedgerView(string workspaceId)
    {
        var account = GetOrCreateAccount(workspaceId);
        return ToLedgerResponse(account);
    }

    public WorkspaceBillingViewResponse StartSubscription(string workspaceId, StartSubscriptionCommandRequest request)
    {
        var lockHandle = GetAccountLock(workspaceId);
        lock (lockHandle)
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
        SyncWalletAndPolicy(account, plan, now);

        return ToResponse(account, subscription);
        }
    }

    public WorkspaceBillingViewResponse ChangeSubscription(string workspaceId, ChangeSubscriptionCommandRequest request)
    {
        var lockHandle = GetAccountLock(workspaceId);
        lock (lockHandle)
        {
            var account = GetOrCreateAccount(workspaceId);
            var existing = GetSubscription(account.CurrentSubscriptionId) ?? throw new InvalidOperationException($"Workspace {workspaceId} has no subscription.");
            var plan = ResolvePlan(request.PlanKey, request.BillingInterval, account.OwnerScopeType);
            var now = DateTimeOffset.UtcNow;
            existing.ChangePlan(plan, now);
            account.AttachSubscription(plan, existing);
            SyncWalletAndPolicy(account, plan, now);

            return ToResponse(account, existing);
        }
    }

    public WorkspaceBillingViewResponse CancelSubscription(string workspaceId, CancelSubscriptionCommandRequest request)
    {
        _ = request;
        var lockHandle = GetAccountLock(workspaceId);
        lock (lockHandle)
        {
            var account = GetOrCreateAccount(workspaceId);
            var existing = GetSubscription(account.CurrentSubscriptionId) ?? throw new InvalidOperationException($"Workspace {workspaceId} has no subscription.");
            existing.Cancel(DateTimeOffset.UtcNow);
            account.Cancel();

            return ToResponse(account, existing);
        }
    }

    public WorkspaceLedgerViewResponse ReserveCredits(string workspaceId, ReserveCreditsCommandRequest request)
    {
        ValidateAmount(request.Amount, nameof(request.Amount));
        var lockHandle = GetAccountLock(workspaceId);
        lock (lockHandle)
        {
            var account = GetOrCreateAccount(workspaceId);
            var policy = GetBudgetPolicy(account);
            var wallet = GetWalletBalance(account);
            if (ShouldPauseOnExhaust(policy) && wallet.AvailableCredits < request.Amount)
            {
                throw new InvalidOperationException($"Workspace {workspaceId} has insufficient available credits.");
            }

            var now = DateTimeOffset.UtcNow;
            account.ReserveCredits(request.Amount);
            wallet = wallet with
            {
                AvailableCredits = wallet.AvailableCredits - request.Amount,
                ReservedCredits = wallet.ReservedCredits + request.Amount,
                UpdatedAtUtc = now
            };
            _walletBalances[account.BillingAccountId] = wallet;
            account.SyncWallet(wallet);
            AppendLedgerEntry(account, LedgerEntryType.UsageReserve, request, now);

            return ToLedgerResponse(account);
        }
    }

    public WorkspaceLedgerViewResponse CommitCredits(string workspaceId, CommitCreditsCommandRequest request)
    {
        ValidateAmount(request.Amount, nameof(request.Amount));
        var lockHandle = GetAccountLock(workspaceId);
        lock (lockHandle)
        {
            var account = GetOrCreateAccount(workspaceId);
            var wallet = GetWalletBalance(account);
            if (wallet.ReservedCredits < request.Amount)
            {
                throw new InvalidOperationException($"Workspace {workspaceId} does not have enough reserved credits to commit.");
            }

            var now = DateTimeOffset.UtcNow;
            account.CommitCredits(request.Amount);
            wallet = wallet with
            {
                ReservedCredits = wallet.ReservedCredits - request.Amount,
                UpdatedAtUtc = now
            };
            _walletBalances[account.BillingAccountId] = wallet;
            account.SyncWallet(wallet);
            AppendLedgerEntry(account, LedgerEntryType.UsageCommit, request, now);

            return ToLedgerResponse(account);
        }
    }

    public WorkspaceLedgerViewResponse ReleaseCredits(string workspaceId, ReleaseCreditsCommandRequest request)
    {
        ValidateAmount(request.Amount, nameof(request.Amount));
        var lockHandle = GetAccountLock(workspaceId);
        lock (lockHandle)
        {
            var account = GetOrCreateAccount(workspaceId);
            var wallet = GetWalletBalance(account);
            if (wallet.ReservedCredits < request.Amount)
            {
                throw new InvalidOperationException($"Workspace {workspaceId} does not have enough reserved credits to release.");
            }

            var now = DateTimeOffset.UtcNow;
            account.ReleaseCredits(request.Amount);
            wallet = wallet with
            {
                AvailableCredits = wallet.AvailableCredits + request.Amount,
                ReservedCredits = wallet.ReservedCredits - request.Amount,
                UpdatedAtUtc = now
            };
            _walletBalances[account.BillingAccountId] = wallet;
            account.SyncWallet(wallet);
            AppendLedgerEntry(account, LedgerEntryType.UsageRelease, request, now);

            return ToLedgerResponse(account);
        }
    }

    private BillingAccount GetOrCreateAccount(string workspaceId, BillingAccountOwnerScopeType? scopeType = null)
    {
        var account = _accounts.GetOrAdd(workspaceId, key =>
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
            SyncWalletAndPolicy(account, plan, now);
            return account;
        });

        EnsureAuxiliaryState(account);
        return account;
    }

    private Subscription? GetSubscription(string? subscriptionId) =>
        subscriptionId is null ? null : _subscriptions.TryGetValue(subscriptionId, out var subscription) ? subscription : null;

    private WalletBalance GetWalletBalance(BillingAccount account)
    {
        if (_walletBalances.TryGetValue(account.BillingAccountId, out var wallet))
        {
            return wallet;
        }

        var seededWallet = new WalletBalance(account.CreditBalance, account.ReservedCreditBalance, account.Currency, DateTimeOffset.UtcNow);
        _walletBalances[account.BillingAccountId] = seededWallet;
        return seededWallet;
    }

    private BudgetPolicy GetBudgetPolicy(BillingAccount account)
    {
        if (_budgetPolicies.TryGetValue(account.BillingAccountId, out var policy))
        {
            return policy;
        }

        var plan = ResolveCurrentPlan(account);
        var seededPolicy = BuildBudgetPolicy(account, plan, DateTimeOffset.UtcNow);
        _budgetPolicies[account.BillingAccountId] = seededPolicy;
        return seededPolicy;
    }

    private void SyncWalletAndPolicy(BillingAccount account, PlanDefinition plan, DateTimeOffset timestampUtc)
    {
        var wallet = new WalletBalance(account.CreditBalance, account.ReservedCreditBalance, account.Currency, timestampUtc);
        _walletBalances[account.BillingAccountId] = wallet;
        _budgetPolicies[account.BillingAccountId] = BuildBudgetPolicy(account, plan, timestampUtc);
        _ledgerEntries.TryAdd(account.BillingAccountId, new ConcurrentQueue<LedgerEntry>());
        account.SyncWallet(wallet);
    }

    private void EnsureAuxiliaryState(BillingAccount account)
    {
        var plan = ResolveCurrentPlan(account);
        if (!_walletBalances.ContainsKey(account.BillingAccountId) || !_budgetPolicies.ContainsKey(account.BillingAccountId))
        {
            SyncWalletAndPolicy(account, plan, DateTimeOffset.UtcNow);
        }

        _ledgerEntries.TryAdd(account.BillingAccountId, new ConcurrentQueue<LedgerEntry>());
    }

    private PlanDefinition ResolveCurrentPlan(BillingAccount account) =>
        _plans.First(plan => string.Equals(plan.PlanKey, account.CurrentPlanKey, StringComparison.OrdinalIgnoreCase) && plan.Version == account.CurrentPlanVersion);

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

    private static BudgetPolicy BuildBudgetPolicy(BillingAccount account, PlanDefinition plan, DateTimeOffset timestampUtc) =>
        new(
            "BillingAccount",
            account.BillingAccountId,
            "usage",
            plan.BillingInterval == BillingInterval.Monthly ? "monthly" : "yearly",
            plan.IncludedCredits,
            plan.DefaultBudgetBehavior == BudgetBehavior.PauseOnExhaust ? "PauseOnExhaust" : "AllowOverage",
            timestampUtc);

    private static bool ShouldPauseOnExhaust(BudgetPolicy policy) =>
        string.Equals(policy.EnforcementMode, "PauseOnExhaust", StringComparison.OrdinalIgnoreCase);

    private static void ValidateAmount(decimal amount, string paramName)
    {
        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(paramName, "Amount must be greater than zero.");
        }
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

    private WorkspaceLedgerViewResponse ToLedgerResponse(BillingAccount account)
    {
        var wallet = GetWalletBalance(account);
        var policy = GetBudgetPolicy(account);
        var ledgerEntries = _ledgerEntries.TryGetValue(account.BillingAccountId, out var entries)
            ? entries.Reverse().Select(MapLedgerEntry).ToArray()
            : Array.Empty<LedgerEntryResponse>();

        return new WorkspaceLedgerViewResponse(MapAccount(account), MapWallet(wallet), MapPolicy(policy), ledgerEntries);
    }

    private static BillingPlanResponse MapPlan(PlanDefinition plan) =>
        new(plan.PlanKey, plan.Version, plan.Family, plan.BillingInterval, plan.StripePriceId, plan.IncludedCredits, plan.Entitlements, plan.DefaultBudgetBehavior, plan.Recommended);

    private static BillingAccountResponse MapAccount(BillingAccount account) =>
        new(account.BillingAccountId, account.WorkspaceId, account.OwnerScopeType, account.OwnerScopeId, account.Status, account.Currency, account.CurrentPlanKey, account.CurrentPlanVersion, account.CurrentSubscriptionId, account.SharedPoolMode, account.CreditBalance, account.ReservedCreditBalance, account.AllocationPolicyId, account.CreatedAtUtc);

    private static WalletBalanceResponse MapWallet(WalletBalance wallet) =>
        new(wallet.AvailableCredits, wallet.ReservedCredits, wallet.Currency, wallet.UpdatedAtUtc);

    private static BudgetPolicyResponse MapPolicy(BudgetPolicy policy) =>
        new(policy.ScopeType, policy.ScopeRefId, policy.Category, policy.Period, policy.LimitAmount, policy.EnforcementMode, policy.CreatedAtUtc);

    private static SubscriptionResponse MapSubscription(Subscription subscription) =>
        new(subscription.SubscriptionId, subscription.BillingAccountId, subscription.Provider, subscription.ProviderSubscriptionId, subscription.Status, subscription.PlanKey, subscription.PlanVersion, subscription.BillingInterval, subscription.CurrentPeriodStartUtc, subscription.CurrentPeriodEndUtc, subscription.CancelAtPeriodEnd);

    private void AppendLedgerEntry<TRequest>(
        BillingAccount account,
        LedgerEntryType type,
        TRequest request,
        DateTimeOffset timestampUtc)
        where TRequest : notnull
    {
        var entry = new LedgerEntry(
            LedgerEntryId: $"led_{Guid.NewGuid():N}",
            BillingAccountId: account.BillingAccountId,
            Type: type,
            Amount: request switch
            {
                ReserveCreditsCommandRequest reserve => reserve.Amount,
                CommitCreditsCommandRequest commit => commit.Amount,
                ReleaseCreditsCommandRequest release => release.Amount,
                _ => throw new InvalidOperationException("Unsupported ledger command.")
            },
            Currency: account.Currency,
            ReferenceId: request switch
            {
                ReserveCreditsCommandRequest reserve => reserve.ReferenceId,
                CommitCreditsCommandRequest commit => commit.ReferenceId,
                ReleaseCreditsCommandRequest release => release.ReferenceId,
                _ => throw new InvalidOperationException("Unsupported ledger command.")
            },
            ProjectId: request switch
            {
                ReserveCreditsCommandRequest reserve => reserve.ProjectId,
                CommitCreditsCommandRequest commit => commit.ProjectId,
                ReleaseCreditsCommandRequest release => release.ProjectId,
                _ => null
            },
            UserId: request switch
            {
                ReserveCreditsCommandRequest reserve => reserve.UserId,
                CommitCreditsCommandRequest commit => commit.UserId,
                ReleaseCreditsCommandRequest release => release.UserId,
                _ => null
            },
            AllocationId: request switch
            {
                ReserveCreditsCommandRequest reserve => reserve.AllocationId,
                CommitCreditsCommandRequest commit => commit.AllocationId,
                ReleaseCreditsCommandRequest release => release.AllocationId,
                _ => null
            },
            CreatedAtUtc: timestampUtc);

        var queue = _ledgerEntries.GetOrAdd(account.BillingAccountId, _ => new ConcurrentQueue<LedgerEntry>());
        queue.Enqueue(entry);
    }

    private static LedgerEntryResponse MapLedgerEntry(LedgerEntry entry) =>
        new(entry.LedgerEntryId, entry.BillingAccountId, entry.Type, entry.Amount, entry.Currency, entry.ReferenceId, entry.ProjectId, entry.UserId, entry.AllocationId, entry.CreatedAtUtc);

    private object GetAccountLock(string workspaceId)
    {
        var accountId = $"ba_{workspaceId}";
        return _accountLocks.GetOrAdd(accountId, _ => new object());
    }
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

    public Task<WorkspaceLedgerViewResponse> GetWorkspaceLedgerViewAsync(string workspaceId, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        return Task.FromResult(_store.GetWorkspaceLedgerView(workspaceId));
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

    public Task<WorkspaceLedgerViewResponse> ReserveCreditsAsync(string workspaceId, ReserveCreditsCommandRequest request, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        return Task.FromResult(_store.ReserveCredits(workspaceId, request));
    }

    public Task<WorkspaceLedgerViewResponse> CommitCreditsAsync(string workspaceId, CommitCreditsCommandRequest request, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        return Task.FromResult(_store.CommitCredits(workspaceId, request));
    }

    public Task<WorkspaceLedgerViewResponse> ReleaseCreditsAsync(string workspaceId, ReleaseCreditsCommandRequest request, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        return Task.FromResult(_store.ReleaseCredits(workspaceId, request));
    }
}
