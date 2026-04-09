using System.ComponentModel.DataAnnotations;
using Graphode.BillingEntitlementsService.Contracts.Common;
using Graphode.BillingEntitlementsService.Contracts.ReferenceItems;

namespace Graphode.BillingEntitlementsService.Application.Services;

public sealed class ReferenceItemRequestValidator
{
    private static readonly HashSet<string> AllowedSortFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "name",
        "status",
        "createdAtUtc",
        "updatedAtUtc"
    };

    private static readonly HashSet<string> AllowedFilterFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "id",
        "workspaceId",
        "name",
        "status",
        "tag",
        "createdAtUtc"
    };

    public void ValidateForCreate(CreateReferenceItemRequest request)
    {
        ValidateAnnotations(request);

        if (request.Tags.Count > 10)
        {
            throw new ContractValidationException(["At most 10 tags are allowed for a reference item."]);
        }
    }

    public void ValidateForList(ListReferenceItemsRequest request)
    {
        ValidateAnnotations(request);

        var errors = new List<string>();

        foreach (var sort in request.Sort)
        {
            if (!AllowedSortFields.Contains(sort.Field))
            {
                errors.Add($"Unsupported sort field '{sort.Field}'.");
            }
        }

        foreach (var filter in request.Filters)
        {
            if (!AllowedFilterFields.Contains(filter.Field))
            {
                errors.Add($"Unsupported filter field '{filter.Field}'.");
            }

            if (filter.Values.Count == 0)
            {
                errors.Add($"Filter '{filter.Field}' must contain at least one value.");
            }

            if (filter.Operator == FilterOperator.Contains && !string.Equals(filter.Field, "name", StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"Operator '{FilterOperator.Contains}' is only supported for the 'name' field in this contract.");
            }

            if (filter.Operator == FilterOperator.StartsWith && !string.Equals(filter.Field, "name", StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"Operator '{FilterOperator.StartsWith}' is only supported for the 'name' field in this contract.");
            }
        }

        if (errors.Count > 0)
        {
            throw new ContractValidationException(errors);
        }
    }

    public void ValidateForArchiveDispatch(ArchiveReferenceItemCommandRequest request)
    {
        ValidateAnnotations(request);
    }

    private static void ValidateAnnotations(object request)
    {
        var validationResults = new List<ValidationResult>();
        var context = new ValidationContext(request);

        if (Validator.TryValidateObject(request, context, validationResults, true))
        {
            return;
        }

        throw new ContractValidationException(validationResults.Select(result => result.ErrorMessage ?? "Validation error."));
    }
}

public sealed class ContractValidationException(IEnumerable<string> errors) : Exception("Contract validation failed.")
{
    public IReadOnlyList<string> Errors { get; } = errors.ToArray();
}
