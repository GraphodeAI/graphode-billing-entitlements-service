using FluentAssertions;
using Graphode.BillingEntitlementsService.Application.Services;
using Graphode.BillingEntitlementsService.Contracts.Billing;
using Graphode.BillingEntitlementsService.Domain.Billing;

namespace Graphode.BillingEntitlementsService.Tests.Billing;

public sealed class BillingCatalogServiceTests
{
    [Fact]
    public async Task GetWorkspaceBillingViewAsync_returns_seeded_account_and_plans()
    {
        var service = new BillingWorkspaceService(new BillingCatalogStore());

        var response = await service.GetWorkspaceBillingViewAsync("graphode-labs", CancellationToken.None);

        response.Account.WorkspaceId.Should().Be("graphode-labs");
        response.Account.Status.Should().Be(BillingAccountStatus.Active);
        response.Plans.Plans.Should().ContainSingle(plan => plan.PlanKey == "SOLO_STARTER");
        response.Subscription.Should().NotBeNull();
        response.Subscription!.Status.Should().Be(SubscriptionStatus.Active);
    }

    [Fact]
    public async Task StartSubscriptionAsync_switches_plan_for_compatible_workspace()
    {
        var service = new BillingWorkspaceService(new BillingCatalogStore());

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
        var service = new BillingWorkspaceService(new BillingCatalogStore());

        var act = async () => await service.StartSubscriptionAsync(
            "graphode-labs",
            new StartSubscriptionCommandRequest("ORG_PRO", BillingInterval.Monthly, null, BillingAccountOwnerScopeType.PersonalWorkspace),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task CancelSubscriptionAsync_marks_subscription_cancelled()
    {
        var service = new BillingWorkspaceService(new BillingCatalogStore());
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
        var service = new BillingWorkspaceService(new BillingCatalogStore());

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
        var service = new BillingWorkspaceService(new BillingCatalogStore());

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
        var service = new BillingWorkspaceService(new BillingCatalogStore());

        var act = async () => await service.ReserveCreditsAsync(
            "graphode-labs",
            new ReserveCreditsCommandRequest(5000, "run_over", null, null, null),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task ReserveCreditsAsync_allows_overage_for_allow_overage_plan()
    {
        var service = new BillingWorkspaceService(new BillingCatalogStore());
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
}
