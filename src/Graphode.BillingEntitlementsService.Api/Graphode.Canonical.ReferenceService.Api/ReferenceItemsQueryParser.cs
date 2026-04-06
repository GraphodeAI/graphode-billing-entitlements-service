using Graphode.BillingEntitlementsService.Contracts.Common;
using Graphode.BillingEntitlementsService.Contracts.ReferenceItems;

namespace Graphode.BillingEntitlementsService.Api;

internal sealed class ReferenceItemsQueryInput
{
    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 20;

    public string[] Sort { get; init; } = [];

    public string[] Filter { get; init; } = [];
}

internal static class ReferenceItemsQueryParser
{
    public static (ListReferenceItemsRequest? Request, IReadOnlyList<string> Errors) Parse(ReferenceItemsQueryInput input)
    {
        var errors = new List<string>();
        var sortDescriptors = new List<SortDescriptor>();
        var filterDescriptors = new List<FilterDescriptor>();

        foreach (var rawSort in input.Sort)
        {
            var parts = rawSort.Split(':', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length is < 1 or > 2)
            {
                errors.Add($"Invalid sort syntax '{rawSort}'. Use field:asc or field:desc.");
                continue;
            }

            var direction = parts.Length == 1 || string.Equals(parts[1], "asc", StringComparison.OrdinalIgnoreCase)
                ? SortDirection.Asc
                : string.Equals(parts[1], "desc", StringComparison.OrdinalIgnoreCase)
                    ? SortDirection.Desc
                    : (SortDirection?)null;

            if (direction is null)
            {
                errors.Add($"Invalid sort direction in '{rawSort}'.");
                continue;
            }

            sortDescriptors.Add(new SortDescriptor(parts[0], direction.Value));
        }

        foreach (var rawFilter in input.Filter)
        {
            var parts = rawFilter.Split(':', StringSplitOptions.TrimEntries);
            if (parts.Length < 3)
            {
                errors.Add($"Invalid filter syntax '{rawFilter}'. Use field:operator:value.");
                continue;
            }

            if (!Enum.TryParse<FilterOperator>(parts[1], true, out var filterOperator))
            {
                errors.Add($"Invalid filter operator in '{rawFilter}'.");
                continue;
            }

            var values = parts[2].Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (values.Length == 0)
            {
                errors.Add($"Filter '{rawFilter}' does not contain a value.");
                continue;
            }

            filterDescriptors.Add(new FilterDescriptor(parts[0], filterOperator, values));
        }

        if (errors.Count > 0)
        {
            return (null, errors);
        }

        return (new ListReferenceItemsRequest
        {
            Page = input.Page,
            PageSize = input.PageSize,
            Sort = sortDescriptors,
            Filters = filterDescriptors
        }, []);
    }
}
