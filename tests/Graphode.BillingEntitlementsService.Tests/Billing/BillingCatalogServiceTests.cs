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
}
