using FluentAssertions;
using Graphode.BillingEntitlementsService.Application.Abstractions.Persistence;
using Graphode.BillingEntitlementsService.Application.Services;
using Graphode.BillingEntitlementsService.Contracts.Billing;
using Graphode.BillingEntitlementsService.Domain.Billing;

namespace Graphode.BillingEntitlementsService.Tests.Billing;

public sealed class BillingCatalogServiceTests
{
    [Fact]
    public async Task GetWorkspaceBillingViewAsync_returns_seeded_account_and_plans()
    {
        var service = CreateService();

        var response = await service.GetWorkspaceBillingViewAsync("graphode-labs", CancellationToken.None);

        response.Account.WorkspaceId.Should().Be("graphode-labs");
        response.Account.Status.Should().Be(BillingAccountStatus.Active);
        response.Plans.Plans.Should().ContainSingle(plan => plan.PlanKey == "SOLO_STARTER");
        response.Subscription.Should().BeNull();
    }

    [Fact]
    public async Task StartSubscriptionAsync_switches_plan_for_compatible_workspace()
    {
        var service = CreateService();

        var response = await service.StartSubscriptionAsync(
            "org_northwind",
            new StartSubscriptionCommandRequest("ORG_PRO", BillingInterval.Monthly, null, BillingAccountOwnerScopeType.OrganizationWorkspace),
            CancellationToken.None);

        response.Account.CurrentPlanKey.Should().Be("ORG_PRO");
        response.Account.SharedPoolMode.Should().Be(BillingSharedPoolMode.WorkspaceSharedPool);
        response.Subscription.Should().NotBeNull();
        response.Subscription!.PlanKey.Should().Be("ORG_PRO");
    }

    [Fact]
    public async Task StartSubscriptionAsync_rejects_plan_family_mismatch()
    {
        var service = CreateService();

        var act = async () => await service.StartSubscriptionAsync(
            "graphode-labs",
            new StartSubscriptionCommandRequest("ORG_PRO", BillingInterval.Monthly, null, BillingAccountOwnerScopeType.PersonalWorkspace),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task CapturePaymentMethodAsync_is_idempotent_for_same_setup_intent_and_payment_method()
    {
        var repo = new InMemoryBillingWorkspaceRepository();
        var service = CreateService(repo);
        var request = new SetupPaymentMethodCommandRequest(
            "card",
            null,
            null,
            null,
            null,
            true,
            "seti_demo_001",
            "pm_demo_001",
            "cus_demo_001");

        var first = await service.CapturePaymentMethodAsync("graphode-labs", request, CancellationToken.None);
        var second = await service.CapturePaymentMethodAsync("graphode-labs", request, CancellationToken.None);

        first.PaymentMethods.Should().ContainSingle();
        second.PaymentMethods.Should().ContainSingle();
        second.Account.DefaultPaymentMethodRefId.Should().Be(first.Account.DefaultPaymentMethodRefId);
    }

    [Fact]
    public async Task BillingStore_snapshot_can_be_reloaded_across_service_instances()
    {
        var repo = new InMemoryBillingWorkspaceRepository();
        var firstService = CreateService(repo);

        await firstService.CapturePaymentMethodAsync(
            "graphode-labs",
            new SetupPaymentMethodCommandRequest(
                "card",
                null,
                null,
                null,
                null,
                true,
                "seti_demo_002",
                "pm_demo_002",
                "cus_demo_002"),
            CancellationToken.None);

        var secondService = CreateService(repo);
        var response = await secondService.GetWorkspacePaymentMethodsAsync("graphode-labs", CancellationToken.None);

        response.Account.StripeCustomerId.Should().Be("cus_ba_graphode-labs");
        response.PaymentMethods.Should().ContainSingle(method => method.StripePaymentMethodId == "pm_demo_002");
    }

    [Fact]
    public async Task HandleStripeWebhookAsync_marks_past_due_and_is_idempotent()
    {
        var fakeStripeClient = new FakeBillingStripeClient
        {
            WebhookEventToReturn = new StripeWebhookEventResult(
                "evt_demo_001",
                "invoice.payment_failed",
                "cus_ba_graphode-labs",
                "sub_stripe_ba_graphode-labs_SOLO_STARTER",
                "si_graphode-labs",
                "price_solo_starter_monthly",
                SubscriptionStatus.PastDue,
                null,
                null,
                null)
        };
        var service = CreateService(new InMemoryBillingWorkspaceRepository(), fakeStripeClient);

        await service.StartSubscriptionAsync(
            "graphode-labs",
            new StartSubscriptionCommandRequest("SOLO_STARTER", BillingInterval.Monthly, null, BillingAccountOwnerScopeType.PersonalWorkspace),
            CancellationToken.None);

        var first = await service.HandleStripeWebhookAsync("{}", "sig_demo", CancellationToken.None);
        var second = await service.HandleStripeWebhookAsync("{}", "sig_demo", CancellationToken.None);

        first.ProcessingStatus.Should().Be("processed");
        first.BillingAccountId.Should().Be("ba_graphode-labs");
        second.ProcessingStatus.Should().Be("duplicate");

        var view = await service.GetWorkspaceBillingViewAsync("graphode-labs", CancellationToken.None);
        view.Account.Status.Should().Be(BillingAccountStatus.PastDue);
        view.Subscription.Should().NotBeNull();
        view.Subscription!.Status.Should().Be(SubscriptionStatus.PastDue);
    }

    [Fact]
    public async Task CancelSubscriptionAsync_marks_subscription_cancelled()
    {
        var service = CreateService();
        await service.StartSubscriptionAsync(
            "graphode-labs",
            new StartSubscriptionCommandRequest("SOLO_STARTER", BillingInterval.Monthly, null, BillingAccountOwnerScopeType.PersonalWorkspace),
            CancellationToken.None);

        var response = await service.CancelSubscriptionAsync(
            "graphode-labs",
            new CancelSubscriptionCommandRequest("workspace closed"),
            CancellationToken.None);

        response.Account.Status.Should().Be(BillingAccountStatus.Cancelled);
        response.Subscription.Should().NotBeNull();
        response.Subscription!.Status.Should().Be(SubscriptionStatus.Cancelled);
    }

    [Fact]
    public async Task GetWorkspaceLedgerViewAsync_returns_wallet_ledger_and_budget_policy()
    {
        var service = CreateService();

        var response = await service.GetWorkspaceLedgerViewAsync("graphode-labs", CancellationToken.None);

        response.Account.WorkspaceId.Should().Be("graphode-labs");
        response.Wallet.AvailableCredits.Should().Be(2500);
        response.Wallet.ReservedCredits.Should().Be(0);
        response.Policy.ScopeType.Should().Be("BillingAccount");
        response.Policy.EnforcementMode.Should().Be("PauseOnExhaust");
        response.LedgerEntries.Should().BeEmpty();
    }

    [Fact]
    public async Task ReserveCommitAndReleaseCredits_update_wallet_and_ledger()
    {
        var service = CreateService();

        var reserved = await service.ReserveCreditsAsync(
            "graphode-labs",
            new ReserveCreditsCommandRequest(250, "run_001", "prj_001", "usr_001", "alloc_001"),
            CancellationToken.None);

        reserved.Wallet.AvailableCredits.Should().Be(2250);
        reserved.Wallet.ReservedCredits.Should().Be(250);
        reserved.LedgerEntries.Should().ContainSingle(entry => entry.Type == LedgerEntryType.UsageReserve && entry.ReferenceId == "run_001");

        var committed = await service.CommitCreditsAsync(
            "graphode-labs",
            new CommitCreditsCommandRequest(150, "run_001", "prj_001", "usr_001", "alloc_001"),
            CancellationToken.None);

        committed.Wallet.AvailableCredits.Should().Be(2250);
        committed.Wallet.ReservedCredits.Should().Be(100);
        committed.LedgerEntries.Should().Contain(entry => entry.Type == LedgerEntryType.UsageCommit);

        var released = await service.ReleaseCreditsAsync(
            "graphode-labs",
            new ReleaseCreditsCommandRequest(100, "run_001", "prj_001", "usr_001", "alloc_001"),
            CancellationToken.None);

        released.Wallet.AvailableCredits.Should().Be(2350);
        released.Wallet.ReservedCredits.Should().Be(0);
        released.LedgerEntries.Should().Contain(entry => entry.Type == LedgerEntryType.UsageRelease);
    }

    [Fact]
    public async Task ReserveCreditsAsync_blocks_pause_on_exhaust_overages()
    {
        var service = CreateService();

        var act = async () => await service.ReserveCreditsAsync(
            "graphode-labs",
            new ReserveCreditsCommandRequest(5000, "run_over", null, null, null),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task ReserveCreditsAsync_allows_overage_for_allow_overage_plan()
    {
        var service = CreateService();
        await service.StartSubscriptionAsync(
            "org_northwind",
            new StartSubscriptionCommandRequest("ORG_PRO", BillingInterval.Monthly, null, BillingAccountOwnerScopeType.OrganizationWorkspace),
            CancellationToken.None);

        var response = await service.ReserveCreditsAsync(
            "org_northwind",
            new ReserveCreditsCommandRequest(12000, "run_002", "prj_002", "usr_002", "alloc_002"),
            CancellationToken.None);

        response.Wallet.AvailableCredits.Should().Be(-2000);
        response.Wallet.ReservedCredits.Should().Be(12000);
        response.Policy.EnforcementMode.Should().Be("AllowOverage");
    }

    private static BillingWorkspaceService CreateService(
        IBillingWorkspaceRepository? repository = null,
        FakeBillingStripeClient? stripeClient = null) =>
        new(new BillingCatalogStore(stripeClient ?? new FakeBillingStripeClient(), repository ?? new InMemoryBillingWorkspaceRepository()));

    private sealed class InMemoryBillingWorkspaceRepository : IBillingWorkspaceRepository
    {
        private readonly Dictionary<string, BillingWorkspaceSnapshot> _snapshots = new(StringComparer.OrdinalIgnoreCase);

        public BillingWorkspaceSnapshot? GetByWorkspaceId(string workspaceId) =>
            _snapshots.TryGetValue(workspaceId, out var snapshot) ? snapshot : null;

        public BillingWorkspaceSnapshot? FindByStripeCustomerId(string stripeCustomerId) =>
            _snapshots.Values.FirstOrDefault(snapshot =>
                string.Equals(snapshot.StripeCustomerId, stripeCustomerId, StringComparison.OrdinalIgnoreCase));

        public BillingWorkspaceSnapshot? FindByProviderSubscriptionId(string providerSubscriptionId) =>
            _snapshots.Values.FirstOrDefault(snapshot =>
                snapshot.Subscription is not null &&
                string.Equals(snapshot.Subscription.ProviderSubscriptionId, providerSubscriptionId, StringComparison.OrdinalIgnoreCase));

        public void Upsert(BillingWorkspaceSnapshot snapshot) => _snapshots[snapshot.WorkspaceId] = snapshot;
    }

    private sealed class FakeBillingStripeClient : IBillingStripeClient
    {
        public StripeWebhookEventResult WebhookEventToReturn { get; init; } =
            new("evt_default", "customer.subscription.updated", "cus_default", "sub_default", "si_default", "price_solo_starter_monthly", SubscriptionStatus.Active, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMonths(1), false);

        public StripeCustomerResult EnsureCustomer(BillingAccount account) =>
            new(account.StripeCustomerId ?? $"cus_{account.BillingAccountId}");

        public string ResolvePriceId(PlanDefinition plan) => plan.StripePriceId;

        public StripeSetupIntentResult CreateSetupIntent(BillingAccount account)
        {
            var customerId = account.StripeCustomerId ?? $"cus_{account.BillingAccountId}";
            return new StripeSetupIntentResult(customerId, $"seti_{account.BillingAccountId}", $"secret_{account.BillingAccountId}");
        }

        public StripePaymentMethodResult ConfirmPaymentMethod(BillingAccount account, string setupIntentId, string stripePaymentMethodId) =>
            new(
                account.StripeCustomerId ?? $"cus_{account.BillingAccountId}",
                stripePaymentMethodId,
                "card",
                "Visa",
                "4242",
                12,
                2030);

        public StripeSubscriptionResult CreateSubscription(BillingAccount account, PlanDefinition plan, string? defaultPaymentMethodId) =>
            new(
                $"sub_stripe_{account.BillingAccountId}_{plan.PlanKey}",
                $"si_{account.BillingAccountId}_{plan.PlanKey}",
                SubscriptionStatus.Active,
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow.AddMonths(1),
                false);

        public StripeSubscriptionResult ChangeSubscription(BillingAccount account, Subscription subscription, PlanDefinition plan, string? defaultPaymentMethodId) =>
            new(
                subscription.ProviderSubscriptionId,
                subscription.StripeSubscriptionItemId ?? string.Empty,
                SubscriptionStatus.Active,
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow.AddMonths(1),
                false);

        public StripeSubscriptionResult CancelSubscription(Subscription subscription) =>
            new(
                subscription.ProviderSubscriptionId,
                subscription.StripeSubscriptionItemId ?? string.Empty,
                SubscriptionStatus.Cancelled,
                subscription.CurrentPeriodStartUtc,
                subscription.CurrentPeriodEndUtc,
                true);

        public StripeWebhookEventResult ParseWebhookEvent(string payload, string signatureHeader)
        {
            _ = payload;
            _ = signatureHeader;
            return WebhookEventToReturn;
        }
    }
}
