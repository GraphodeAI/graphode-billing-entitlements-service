using Graphode.BillingEntitlementsService.Application.Abstractions.Persistence;
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
    WorkspacePaymentMethodsResponse GetWorkspacePaymentMethods(string workspaceId);
    WorkspaceBillingViewResponse StartSubscription(string workspaceId, StartSubscriptionCommandRequest request);
    WorkspaceBillingViewResponse ChangeSubscription(string workspaceId, ChangeSubscriptionCommandRequest request);
    WorkspaceBillingViewResponse CancelSubscription(string workspaceId, CancelSubscriptionCommandRequest request);
    WorkspacePaymentMethodsResponse CapturePaymentMethod(string workspaceId, SetupPaymentMethodCommandRequest request);
    StripeWebhookProcessingResponse HandleStripeWebhook(string payload, string signatureHeader);
    WorkspaceLedgerViewResponse ReserveCredits(string workspaceId, ReserveCreditsCommandRequest request);
    WorkspaceLedgerViewResponse CommitCredits(string workspaceId, CommitCreditsCommandRequest request);
    WorkspaceLedgerViewResponse ReleaseCredits(string workspaceId, ReleaseCreditsCommandRequest request);
}

public sealed class BillingCatalogStore : IBillingCatalogStore
{
    private readonly IBillingStripeClient _stripeClient;
    private readonly IBillingWorkspaceRepository _workspaceRepository;
    private readonly ConcurrentDictionary<string, BillingAccount> _accounts = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, Subscription> _subscriptions = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, WalletBalance> _walletBalances = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, BudgetPolicy> _budgetPolicies = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, ConcurrentQueue<LedgerEntry>> _ledgerEntries = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, List<PaymentMethodRef>> _paymentMethods = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, HashSet<string>> _processedStripeEvents = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, object> _accountLocks = new(StringComparer.OrdinalIgnoreCase);
    private readonly IReadOnlyList<PlanDefinition> _plans = BillingSeed.Plans;

    public BillingCatalogStore(IBillingStripeClient stripeClient, IBillingWorkspaceRepository workspaceRepository)
    {
        _stripeClient = stripeClient;
        _workspaceRepository = workspaceRepository;
    }

    public IReadOnlyList<PlanDefinition> ListPlans() =>
        _plans.Select(plan => plan with { StripePriceId = _stripeClient.ResolvePriceId(plan) }).ToArray();

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

    public WorkspacePaymentMethodsResponse GetWorkspacePaymentMethods(string workspaceId)
    {
        var account = GetOrCreateAccount(workspaceId);
        return ToPaymentMethodsResponse(account);
    }

    public WorkspaceBillingViewResponse StartSubscription(string workspaceId, StartSubscriptionCommandRequest request)
    {
        var lockHandle = GetAccountLock(workspaceId);
        lock (lockHandle)
        {
            var account = GetOrCreateAccount(workspaceId, request.OwnerScopeType);
            var plan = ResolvePlan(request.PlanKey, request.BillingInterval, account.OwnerScopeType);
            var now = DateTimeOffset.UtcNow;
            var stripeCustomer = EnsureStripeCustomer(account);
            var subscriptionResult = _stripeClient.CreateSubscription(
                account,
                plan,
                account.DefaultPaymentMethodRefId is null
                    ? null
                    : GetPaymentMethod(account, account.DefaultPaymentMethodRefId)?.StripePaymentMethodId);

            var subscriptionId = account.CurrentSubscriptionId ?? $"sub_{account.BillingAccountId}";
            var subscription = new Subscription(
                subscriptionId,
                account.BillingAccountId,
                subscriptionResult.StripeSubscriptionId,
                subscriptionResult.StripeSubscriptionItemId,
                subscriptionResult.Status,
                plan.PlanKey,
                plan.Version,
                plan.BillingInterval,
                subscriptionResult.CurrentPeriodStartUtc,
                subscriptionResult.CurrentPeriodEndUtc,
                cancelAtPeriodEnd: subscriptionResult.CancelAtPeriodEnd);

            _subscriptions[subscription.SubscriptionId] = subscription;
            account.SetStripeCustomerId(stripeCustomer.StripeCustomerId);
            account.AttachSubscription(plan, subscription);
            SyncWalletAndPolicy(account, plan, now);
            PersistWorkspace(account);

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
            var subscriptionResult = _stripeClient.ChangeSubscription(
                account,
                existing,
                plan,
                account.DefaultPaymentMethodRefId is null
                    ? null
                    : GetPaymentMethod(account, account.DefaultPaymentMethodRefId)?.StripePaymentMethodId);
            existing.ChangePlan(plan, subscriptionResult.CurrentPeriodStartUtc);
            existing.SetStripeSubscriptionItemId(subscriptionResult.StripeSubscriptionItemId);
            account.AttachSubscription(plan, existing);
            SyncWalletAndPolicy(account, plan, subscriptionResult.CurrentPeriodStartUtc);
            PersistWorkspace(account);

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
            _stripeClient.CancelSubscription(existing);
            existing.Cancel(DateTimeOffset.UtcNow);
            account.Cancel();
            PersistWorkspace(account);

            return ToResponse(account, existing);
        }
    }

    public WorkspacePaymentMethodsResponse CapturePaymentMethod(string workspaceId, SetupPaymentMethodCommandRequest request)
    {
        ValidatePaymentMethodRequest(request);
        var lockHandle = GetAccountLock(workspaceId);
        lock (lockHandle)
        {
            var account = GetOrCreateAccount(workspaceId);
            if (string.IsNullOrWhiteSpace(request.SetupIntentId) || string.IsNullOrWhiteSpace(request.StripePaymentMethodId))
            {
                var setupIntent = _stripeClient.CreateSetupIntent(account);
                account.SetStripeCustomerId(setupIntent.StripeCustomerId);
                PersistWorkspace(account);
                return ToPaymentMethodsResponse(account, setupIntent.SetupIntentId, setupIntent.ClientSecret, setupIntent.StripeCustomerId);
            }

            var paymentMethods = _paymentMethods.GetOrAdd(account.BillingAccountId, _ => new List<PaymentMethodRef>());
            var now = DateTimeOffset.UtcNow;
            var shouldBeDefault = request.IsDefault || paymentMethods.Count == 0 || account.DefaultPaymentMethodRefId is null;
            var existingPaymentMethod = paymentMethods.FirstOrDefault(method =>
                string.Equals(method.StripePaymentMethodId, request.StripePaymentMethodId!, StringComparison.OrdinalIgnoreCase));

            if (existingPaymentMethod is not null)
            {
                if (shouldBeDefault)
                {
                    for (var index = 0; index < paymentMethods.Count; index++)
                    {
                        paymentMethods[index] = paymentMethods[index] with
                        {
                            IsDefault = string.Equals(
                                paymentMethods[index].PaymentMethodRefId,
                                existingPaymentMethod.PaymentMethodRefId,
                                StringComparison.OrdinalIgnoreCase)
                        };
                    }

                    account.SetDefaultPaymentMethod(existingPaymentMethod.PaymentMethodRefId);
                }
                PersistWorkspace(account);
                return ToPaymentMethodsResponse(account);
            }

            if (shouldBeDefault)
            {
                for (var index = 0; index < paymentMethods.Count; index++)
                {
                    paymentMethods[index] = paymentMethods[index] with { IsDefault = false };
                }
            }

            var paymentMethodResult = _stripeClient.ConfirmPaymentMethod(
                account,
                request.SetupIntentId!,
                request.StripePaymentMethodId!);

            var paymentMethod = new PaymentMethodRef(
                $"pmr_{Guid.NewGuid():N}",
                account.BillingAccountId,
                paymentMethodResult.StripePaymentMethodId,
                paymentMethodResult.Type,
                paymentMethodResult.Brand,
                paymentMethodResult.Last4,
                paymentMethodResult.ExpMonth,
                paymentMethodResult.ExpYear,
                shouldBeDefault,
                PaymentMethodRefStatus.Active,
                now);

            paymentMethods.Add(paymentMethod);
            if (shouldBeDefault)
            {
                account.SetDefaultPaymentMethod(paymentMethod.PaymentMethodRefId);
            }

            account.SetStripeCustomerId(paymentMethodResult.StripeCustomerId);
            PersistWorkspace(account);

            return ToPaymentMethodsResponse(account);
        }
    }

    public StripeWebhookProcessingResponse HandleStripeWebhook(string payload, string signatureHeader)
    {
        var stripeEvent = _stripeClient.ParseWebhookEvent(payload, signatureHeader);
        var account = FindAccountForStripeEvent(stripeEvent);

        if (account is null)
        {
            return new StripeWebhookProcessingResponse(
                stripeEvent.EventId,
                stripeEvent.EventType,
                "ignored",
                null,
                null,
                "No billing account matched the Stripe customer or subscription identifier.");
        }

        var lockHandle = GetAccountLock(account.WorkspaceId);
        lock (lockHandle)
        {
            account = FindAccountForStripeEvent(stripeEvent) ?? account;
            if (IsStripeEventProcessed(account, stripeEvent.EventId))
            {
                return new StripeWebhookProcessingResponse(
                    stripeEvent.EventId,
                    stripeEvent.EventType,
                    "duplicate",
                    account.BillingAccountId,
                    account.CurrentSubscriptionId,
                    "Event id already processed.");
            }

            var response = ApplyStripeEvent(account, stripeEvent);
            MarkStripeEventProcessed(account, stripeEvent.EventId);
            PersistWorkspace(account);

            return response;
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
            PersistWorkspace(account);

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
            PersistWorkspace(account);

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
            PersistWorkspace(account);

            return ToLedgerResponse(account);
        }
    }

    private BillingAccount GetOrCreateAccount(string workspaceId, BillingAccountOwnerScopeType? scopeType = null)
    {
        var lockHandle = GetAccountLock(workspaceId);
        lock (lockHandle)
        {
            if (_accounts.TryGetValue(workspaceId, out var cachedAccount))
            {
                if (scopeType.HasValue && cachedAccount.OwnerScopeType != scopeType.Value)
                {
                    throw new InvalidOperationException($"Workspace {workspaceId} already exists with scope {cachedAccount.OwnerScopeType}.");
                }

                EnsureAuxiliaryState(cachedAccount);
                return cachedAccount;
            }

            var persisted = _workspaceRepository.GetByWorkspaceId(workspaceId);
            if (persisted is not null)
            {
                if (scopeType.HasValue && persisted.OwnerScopeType != scopeType.Value)
                {
                    throw new InvalidOperationException($"Workspace {workspaceId} already exists with scope {persisted.OwnerScopeType}.");
                }

                return RestoreWorkspace(persisted);
            }

            var ownerScopeType = scopeType ?? InferScopeType(workspaceId);
            var plan = BillingSeed.DefaultPlanFor(ownerScopeType);
            var now = DateTimeOffset.UtcNow;
            var account = new BillingAccount(
                billingAccountId: $"ba_{workspaceId}",
                workspaceId: workspaceId,
                ownerScopeType: ownerScopeType,
                ownerScopeId: workspaceId,
                status: BillingAccountStatus.Active,
                currency: "EUR",
                currentPlanKey: plan.PlanKey,
                currentPlanVersion: plan.Version,
                currentSubscriptionId: null,
                stripeCustomerId: null,
                sharedPoolMode: ownerScopeType == BillingAccountOwnerScopeType.OrganizationWorkspace
                    ? BillingSharedPoolMode.WorkspaceSharedPool
                    : BillingSharedPoolMode.None,
                creditBalance: plan.IncludedCredits,
                reservedCreditBalance: 0,
                defaultPaymentMethodRefId: null,
                allocationPolicyId: ownerScopeType == BillingAccountOwnerScopeType.OrganizationWorkspace ? $"cap_{workspaceId}" : null,
                createdAtUtc: now);

            var subscription = new Subscription(
                subscriptionId: $"sub_{workspaceId}",
                billingAccountId: account.BillingAccountId,
                providerSubscriptionId: $"sub_stripe_{workspaceId}",
                stripeSubscriptionItemId: $"si_{workspaceId}",
                status: SubscriptionStatus.Active,
                planKey: plan.PlanKey,
                planVersion: plan.Version,
                billingInterval: plan.BillingInterval,
                currentPeriodStartUtc: now,
                currentPeriodEndUtc: now.AddMonths(1),
                cancelAtPeriodEnd: false);

            _accounts[workspaceId] = account;
            _subscriptions[subscription.SubscriptionId] = subscription;
            SyncWalletAndPolicy(account, plan, now);
            PersistWorkspace(account);
            return account;
        }
    }

    private BillingAccount? FindAccountForStripeEvent(StripeWebhookEventResult stripeEvent)
    {
        if (!string.IsNullOrWhiteSpace(stripeEvent.StripeCustomerId))
        {
            var customerAccount = FindAccountByStripeCustomerId(stripeEvent.StripeCustomerId);
            if (customerAccount is not null)
            {
                return customerAccount;
            }
        }

        if (!string.IsNullOrWhiteSpace(stripeEvent.StripeSubscriptionId))
        {
            var subscriptionAccount = FindAccountByProviderSubscriptionId(stripeEvent.StripeSubscriptionId);
            if (subscriptionAccount is not null)
            {
                return subscriptionAccount;
            }
        }

        return null;
    }

    private BillingAccount? FindAccountByStripeCustomerId(string stripeCustomerId)
    {
        var cachedAccount = _accounts.Values.FirstOrDefault(account =>
            string.Equals(account.StripeCustomerId, stripeCustomerId, StringComparison.OrdinalIgnoreCase));
        if (cachedAccount is not null)
        {
            return cachedAccount;
        }

        var snapshot = _workspaceRepository.FindByStripeCustomerId(stripeCustomerId);
        return snapshot is null ? null : RestoreWorkspace(snapshot);
    }

    private BillingAccount? FindAccountByProviderSubscriptionId(string providerSubscriptionId)
    {
        var cachedSubscription = _subscriptions.Values.FirstOrDefault(subscription =>
            string.Equals(subscription.ProviderSubscriptionId, providerSubscriptionId, StringComparison.OrdinalIgnoreCase));
        if (cachedSubscription is not null)
        {
            return _accounts.Values.FirstOrDefault(account => account.BillingAccountId == cachedSubscription.BillingAccountId);
        }

        var snapshot = _workspaceRepository.FindByProviderSubscriptionId(providerSubscriptionId);
        return snapshot is null ? null : RestoreWorkspace(snapshot);
    }

    private StripeWebhookProcessingResponse ApplyStripeEvent(BillingAccount account, StripeWebhookEventResult stripeEvent)
    {
        var now = stripeEvent.CurrentPeriodEndUtc ?? DateTimeOffset.UtcNow;
        var subscription = account.CurrentSubscriptionId is null ? null : GetSubscription(account.CurrentSubscriptionId);

        if (subscription is not null && !string.IsNullOrWhiteSpace(stripeEvent.StripeSubscriptionId) &&
            !string.Equals(subscription.ProviderSubscriptionId, stripeEvent.StripeSubscriptionId, StringComparison.OrdinalIgnoreCase))
        {
            subscription = null;
        }

        switch (stripeEvent.EventType.ToLowerInvariant())
        {
            case "customer.subscription.created":
            case "customer.subscription.updated":
                ApplySubscriptionUpdate(account, subscription, stripeEvent, now);
                break;
            case "customer.subscription.deleted":
                ApplySubscriptionCancellation(account, subscription, now, stripeEvent.EventType);
                break;
            case "invoice.payment_failed":
                ApplyPastDueState(account, subscription, now, "Invoice payment failed.");
                break;
            case "invoice.payment_succeeded":
            case "invoice.paid":
                ApplyActiveState(account, subscription, now, "Invoice paid.");
                break;
            default:
                return new StripeWebhookProcessingResponse(
                    stripeEvent.EventId,
                    stripeEvent.EventType,
                    "ignored",
                    account.BillingAccountId,
                    account.CurrentSubscriptionId,
                    "Stripe event type is not handled by the billing recovery boundary.");
        }

        return new StripeWebhookProcessingResponse(
            stripeEvent.EventId,
            stripeEvent.EventType,
            "processed",
            account.BillingAccountId,
            account.CurrentSubscriptionId,
            $"Applied {stripeEvent.EventType}.");
    }

    private void ApplySubscriptionUpdate(
        BillingAccount account,
        Subscription? subscription,
        StripeWebhookEventResult stripeEvent,
        DateTimeOffset timestampUtc)
    {
        if (subscription is null)
        {
            account.MarkActive();
            return;
        }

        var mappedPlan = ResolvePlanForPriceId(stripeEvent.StripePriceId, account.OwnerScopeType) ?? ResolveCurrentPlan(account);
        if (subscription.PlanKey != mappedPlan.PlanKey || subscription.PlanVersion != mappedPlan.Version)
        {
            subscription.ChangePlan(mappedPlan, stripeEvent.CurrentPeriodStartUtc ?? timestampUtc);
        }

        subscription.ApplyProviderState(
            stripeEvent.SubscriptionStatus ?? SubscriptionStatus.Active,
            stripeEvent.CurrentPeriodStartUtc ?? timestampUtc,
            stripeEvent.CurrentPeriodEndUtc ?? subscription.CurrentPeriodEndUtc,
            stripeEvent.CancelAtPeriodEnd ?? subscription.CancelAtPeriodEnd);

        if (stripeEvent.SubscriptionStatus == SubscriptionStatus.PastDue)
        {
            account.MarkPastDue();
        }
        else if (stripeEvent.SubscriptionStatus == SubscriptionStatus.Cancelled)
        {
            account.Cancel();
        }
        else
        {
            account.MarkActive();
        }
    }

    private void ApplySubscriptionCancellation(
        BillingAccount account,
        Subscription? subscription,
        DateTimeOffset timestampUtc,
        string eventType)
    {
        _ = eventType;
        subscription?.Cancel(timestampUtc);
        account.Cancel();
    }

    private void ApplyPastDueState(BillingAccount account, Subscription? subscription, DateTimeOffset timestampUtc, string reason)
    {
        _ = reason;
        if (subscription is not null)
        {
            subscription.ApplyProviderState(SubscriptionStatus.PastDue, subscription.CurrentPeriodStartUtc, timestampUtc, subscription.CancelAtPeriodEnd);
        }

        account.MarkPastDue();
    }

    private void ApplyActiveState(BillingAccount account, Subscription? subscription, DateTimeOffset timestampUtc, string reason)
    {
        _ = reason;
        if (subscription is not null)
        {
            subscription.ApplyProviderState(SubscriptionStatus.Active, subscription.CurrentPeriodStartUtc, timestampUtc, false);
        }

        account.MarkActive();
    }

    private PlanDefinition? ResolvePlanForPriceId(string? stripePriceId, BillingAccountOwnerScopeType ownerScopeType)
    {
        if (string.IsNullOrWhiteSpace(stripePriceId))
        {
            return null;
        }

        var expectedFamily = ownerScopeType == BillingAccountOwnerScopeType.OrganizationWorkspace
            ? BillingPlanFamily.Organization
            : BillingPlanFamily.Solo;

        return _plans.FirstOrDefault(plan =>
            plan.Family == expectedFamily &&
            string.Equals(_stripeClient.ResolvePriceId(plan), stripePriceId, StringComparison.OrdinalIgnoreCase));
    }

    private bool IsStripeEventProcessed(BillingAccount account, string eventId) =>
        _processedStripeEvents.TryGetValue(account.BillingAccountId, out var events) && events.Contains(eventId);

    private void MarkStripeEventProcessed(BillingAccount account, string eventId)
    {
        var events = _processedStripeEvents.GetOrAdd(account.BillingAccountId, _ => new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        events.Add(eventId);
    }

    private Subscription? GetSubscription(string? subscriptionId) =>
        subscriptionId is null ? null : _subscriptions.TryGetValue(subscriptionId, out var subscription) ? subscription : null;

    private PaymentMethodRef? GetPaymentMethod(BillingAccount account, string paymentMethodRefId)
    {
        return _paymentMethods.TryGetValue(account.BillingAccountId, out var paymentMethods)
            ? paymentMethods.FirstOrDefault(paymentMethod => paymentMethod.PaymentMethodRefId == paymentMethodRefId)
            : null;
    }

    private StripeCustomerResult EnsureStripeCustomer(BillingAccount account)
    {
        if (!string.IsNullOrWhiteSpace(account.StripeCustomerId))
        {
            return new StripeCustomerResult(account.StripeCustomerId);
        }

        var result = _stripeClient.EnsureCustomer(account);
        account.SetStripeCustomerId(result.StripeCustomerId);
        return result;
    }

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
        _paymentMethods.TryAdd(account.BillingAccountId, new List<PaymentMethodRef>());
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
        _paymentMethods.TryAdd(account.BillingAccountId, new List<PaymentMethodRef>());
        _processedStripeEvents.TryAdd(account.BillingAccountId, []);
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

    private WorkspaceBillingViewResponse ToResponse(BillingAccount account, Subscription? subscription)
    {
        var plans = ListPlans().Select(MapPlan).ToArray();
        return new WorkspaceBillingViewResponse(ToAccountResponse(account), subscription is null ? null : ToSubscriptionResponse(subscription), new BillingPlansResponse(plans));
    }

    private WorkspaceLedgerViewResponse ToLedgerResponse(BillingAccount account)
    {
        var wallet = GetWalletBalance(account);
        var policy = GetBudgetPolicy(account);
        var ledgerEntries = _ledgerEntries.TryGetValue(account.BillingAccountId, out var entries)
            ? entries.Reverse().Select(MapLedgerEntry).ToArray()
            : Array.Empty<LedgerEntryResponse>();

        return new WorkspaceLedgerViewResponse(ToAccountResponse(account), MapWallet(wallet), MapPolicy(policy), ledgerEntries);
    }

    private WorkspacePaymentMethodsResponse ToPaymentMethodsResponse(
        BillingAccount account,
        string? setupIntentId = null,
        string? setupIntentClientSecret = null,
        string? stripeCustomerId = null)
    {
        var paymentMethods = _paymentMethods.TryGetValue(account.BillingAccountId, out var methods)
            ? methods.OrderByDescending(method => method.CreatedAtUtc).Select(MapPaymentMethod).ToArray()
            : Array.Empty<PaymentMethodRefResponse>();

        return new WorkspacePaymentMethodsResponse(
            ToAccountResponse(account),
            paymentMethods,
            setupIntentId,
            setupIntentClientSecret,
            stripeCustomerId ?? account.StripeCustomerId);
    }

    private static BillingPlanResponse MapPlan(PlanDefinition plan) =>
        new(plan.PlanKey, plan.Version, plan.Family, plan.BillingInterval, plan.StripePriceId, plan.BasePrice, plan.SeatPrice, plan.IncludedCredits, plan.Entitlements, plan.DefaultBudgetBehavior, plan.Recommended);

    private static BillingAccountResponse ToAccountResponse(BillingAccount account) =>
        new(account.BillingAccountId, account.WorkspaceId, account.OwnerScopeType, account.OwnerScopeId, account.Status, account.Currency, account.CurrentPlanKey, account.CurrentPlanVersion, account.CurrentSubscriptionId, account.StripeCustomerId, account.SharedPoolMode, account.CreditBalance, account.ReservedCreditBalance, account.DefaultPaymentMethodRefId, account.AllocationPolicyId, account.CreatedAtUtc);

    private static WalletBalanceResponse MapWallet(WalletBalance wallet) =>
        new(wallet.AvailableCredits, wallet.ReservedCredits, wallet.Currency, wallet.UpdatedAtUtc);

    private static BudgetPolicyResponse MapPolicy(BudgetPolicy policy) =>
        new(policy.ScopeType, policy.ScopeRefId, policy.Category, policy.Period, policy.LimitAmount, policy.EnforcementMode, policy.CreatedAtUtc);

    private static SubscriptionResponse ToSubscriptionResponse(Subscription subscription) =>
        new(subscription.SubscriptionId, subscription.BillingAccountId, subscription.Provider, subscription.ProviderSubscriptionId, subscription.StripeSubscriptionItemId, subscription.Status, subscription.PlanKey, subscription.PlanVersion, subscription.BillingInterval, subscription.CurrentPeriodStartUtc, subscription.CurrentPeriodEndUtc, subscription.CancelAtPeriodEnd);

    private static PaymentMethodRefResponse MapPaymentMethod(PaymentMethodRef paymentMethod) =>
        new(
            paymentMethod.PaymentMethodRefId,
            paymentMethod.BillingAccountId,
            paymentMethod.StripePaymentMethodId,
            paymentMethod.Type,
            paymentMethod.Brand,
            paymentMethod.Last4,
            paymentMethod.ExpMonth,
            paymentMethod.ExpYear,
            paymentMethod.IsDefault,
            paymentMethod.Status,
            paymentMethod.CreatedAtUtc);

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

    private BillingAccount RestoreWorkspace(BillingWorkspaceSnapshot snapshot)
    {
        var account = new BillingAccount(
            snapshot.BillingAccountId,
            snapshot.WorkspaceId,
            snapshot.OwnerScopeType,
            snapshot.OwnerScopeId,
            snapshot.Status,
            snapshot.Currency,
            snapshot.CurrentPlanKey,
            snapshot.CurrentPlanVersion,
            snapshot.CurrentSubscriptionId,
            snapshot.StripeCustomerId,
            snapshot.SharedPoolMode,
            snapshot.CreditBalance,
            snapshot.ReservedCreditBalance,
            snapshot.DefaultPaymentMethodRefId,
            snapshot.AllocationPolicyId,
            snapshot.CreatedAtUtc);

        _accounts[snapshot.WorkspaceId] = account;

        if (snapshot.Subscription is not null)
        {
            var subscription = ToDomain(snapshot.Subscription);
            _subscriptions[subscription.SubscriptionId] = subscription;
        }

        _paymentMethods[snapshot.BillingAccountId] = snapshot.PaymentMethods.ToList();
        _ledgerEntries[snapshot.BillingAccountId] = new ConcurrentQueue<LedgerEntry>(snapshot.LedgerEntries);
        _processedStripeEvents[snapshot.BillingAccountId] = snapshot.ProcessedStripeEventIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        _walletBalances[snapshot.BillingAccountId] = new WalletBalance(snapshot.CreditBalance, snapshot.ReservedCreditBalance, snapshot.Currency, snapshot.CreatedAtUtc);
        _budgetPolicies[snapshot.BillingAccountId] = BuildBudgetPolicy(account, ResolveCurrentPlan(account), snapshot.CreatedAtUtc);

        EnsureAuxiliaryState(account);
        return account;
    }

    private void PersistWorkspace(BillingAccount account)
    {
        var snapshot = BuildSnapshot(account);
        _workspaceRepository.Upsert(snapshot);
    }

    private BillingWorkspaceSnapshot BuildSnapshot(BillingAccount account)
    {
        var paymentMethods = _paymentMethods.TryGetValue(account.BillingAccountId, out var methods)
            ? methods.ToArray()
            : [];
        var ledgerEntries = _ledgerEntries.TryGetValue(account.BillingAccountId, out var entries)
            ? entries.ToArray()
            : [];
        var processedStripeEventIds = _processedStripeEvents.TryGetValue(account.BillingAccountId, out var events)
            ? events.ToArray()
            : [];

        return new BillingWorkspaceSnapshot(
            BillingAccountId: account.BillingAccountId,
            WorkspaceId: account.WorkspaceId,
            OwnerScopeType: account.OwnerScopeType,
            OwnerScopeId: account.OwnerScopeId,
            Status: account.Status,
            Currency: account.Currency,
            CurrentPlanKey: account.CurrentPlanKey,
            CurrentPlanVersion: account.CurrentPlanVersion,
            CurrentSubscriptionId: account.CurrentSubscriptionId,
            StripeCustomerId: account.StripeCustomerId,
            SharedPoolMode: account.SharedPoolMode,
            CreditBalance: account.CreditBalance,
            ReservedCreditBalance: account.ReservedCreditBalance,
            DefaultPaymentMethodRefId: account.DefaultPaymentMethodRefId,
            AllocationPolicyId: account.AllocationPolicyId,
            CreatedAtUtc: account.CreatedAtUtc,
            Subscription: GetSubscription(account.CurrentSubscriptionId) is Subscription subscription ? ToSnapshot(subscription) : null,
            PaymentMethods: paymentMethods,
            LedgerEntries: ledgerEntries,
            ProcessedStripeEventIds: processedStripeEventIds);
    }

    private static BillingSubscriptionSnapshot ToSnapshot(Subscription subscription) =>
        new(
            subscription.SubscriptionId,
            subscription.BillingAccountId,
            subscription.ProviderSubscriptionId,
            subscription.StripeSubscriptionItemId,
            subscription.Status,
            subscription.PlanKey,
            subscription.PlanVersion,
            subscription.BillingInterval,
            subscription.CurrentPeriodStartUtc,
            subscription.CurrentPeriodEndUtc,
            subscription.CancelAtPeriodEnd);

    private static Subscription ToDomain(BillingSubscriptionSnapshot snapshot) =>
        new(
            snapshot.SubscriptionId,
            snapshot.BillingAccountId,
            snapshot.ProviderSubscriptionId,
            snapshot.StripeSubscriptionItemId,
            snapshot.Status,
            snapshot.PlanKey,
            snapshot.PlanVersion,
            snapshot.BillingInterval,
            snapshot.CurrentPeriodStartUtc,
            snapshot.CurrentPeriodEndUtc,
            snapshot.CancelAtPeriodEnd);

    private static LedgerEntryResponse MapLedgerEntry(LedgerEntry entry) =>
        new(entry.LedgerEntryId, entry.BillingAccountId, entry.Type, entry.Amount, entry.Currency, entry.ReferenceId, entry.ProjectId, entry.UserId, entry.AllocationId, entry.CreatedAtUtc);

    private object GetAccountLock(string workspaceId)
    {
        var accountId = $"ba_{workspaceId}";
        return _accountLocks.GetOrAdd(accountId, _ => new object());
    }

    private static void ValidatePaymentMethodRequest(SetupPaymentMethodCommandRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Type))
        {
            throw new ArgumentException("Payment method type is required.", nameof(request.Type));
        }

        if (request.ExpMonth.HasValue && (request.ExpMonth.Value < 1 || request.ExpMonth.Value > 12))
        {
            throw new ArgumentOutOfRangeException(nameof(request.ExpMonth), "Expiry month must be between 1 and 12.");
        }

        if (request.ExpYear.HasValue && request.ExpYear.Value < DateTimeOffset.UtcNow.Year)
        {
            throw new ArgumentOutOfRangeException(nameof(request.ExpYear), "Expiry year must be a future year.");
        }

        if (request.Last4 is not null && request.Last4.Length != 4)
        {
            throw new ArgumentOutOfRangeException(nameof(request.Last4), "Last4 must contain exactly 4 digits.");
        }
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
            _store.ListPlans().Select(plan => new BillingPlanResponse(plan.PlanKey, plan.Version, plan.Family, plan.BillingInterval, plan.StripePriceId, plan.BasePrice, plan.SeatPrice, plan.IncludedCredits, plan.Entitlements, plan.DefaultBudgetBehavior, plan.Recommended)).ToArray());
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

    public Task<WorkspacePaymentMethodsResponse> GetWorkspacePaymentMethodsAsync(string workspaceId, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        return Task.FromResult(_store.GetWorkspacePaymentMethods(workspaceId));
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

    public Task<WorkspacePaymentMethodsResponse> CapturePaymentMethodAsync(
        string workspaceId,
        SetupPaymentMethodCommandRequest request,
        CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        return Task.FromResult(_store.CapturePaymentMethod(workspaceId, request));
    }

    public Task<StripeWebhookProcessingResponse> HandleStripeWebhookAsync(
        string payload,
        string signatureHeader,
        CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        return Task.FromResult(_store.HandleStripeWebhook(payload, signatureHeader));
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
