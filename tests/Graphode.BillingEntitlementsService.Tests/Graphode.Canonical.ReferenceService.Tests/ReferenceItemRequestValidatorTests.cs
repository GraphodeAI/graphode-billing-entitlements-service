using Graphode.BillingEntitlementsService.Contracts.Common;
using Graphode.BillingEntitlementsService.Contracts.ReferenceItems;
using FluentAssertions;
using Graphode.BillingEntitlementsService.Application.Services;

namespace Graphode.BillingEntitlementsService.Tests;

public sealed class ReferenceItemRequestValidatorTests
{
    private readonly ReferenceItemRequestValidator _validator = new();

    [Fact]
    public void List_request_accepts_supported_fields()
    {
        var request = new ListReferenceItemsRequest
        {
            Page = 1,
            PageSize = 25,
            Sort = [new SortDescriptor("createdAtUtc", SortDirection.Desc)],
            Filters = [new FilterDescriptor("status", FilterOperator.Eq, ["active"])]
        };

        var action = () => _validator.ValidateForList(request);

        action.Should().NotThrow();
    }

    [Fact]
    public void List_request_rejects_unsupported_fields()
    {
        var request = new ListReferenceItemsRequest
        {
            Filters = [new FilterDescriptor("secretField", FilterOperator.Eq, ["x"])]
        };

        var action = () => _validator.ValidateForList(request);

        action.Should().Throw<ContractValidationException>()
            .Which.Errors.Should().Contain(error => error.Contains("Unsupported filter field", StringComparison.Ordinal));
    }
}
