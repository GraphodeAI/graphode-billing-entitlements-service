using Graphode.BillingEntitlementsService.Contracts.Common;
using Graphode.BillingEntitlementsService.Application.Abstractions.Persistence;
using Graphode.BillingEntitlementsService.Domain.ReferenceItems;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Graphode.BillingEntitlementsService.Infrastructure.Persistence.ReferenceItems;

public sealed class ReferenceItemRepository(IMongoCollectionAccessor collectionAccessor)
    : MongoRepositoryBase<ReferenceItemDocument>(collectionAccessor.GetCollection<ReferenceItemDocument>(CollectionName)),
      IReferenceItemRepository
{
    public const string CollectionName = "reference_items";

    private static readonly IReadOnlyDictionary<string, string> SortFieldMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["name"] = "name",
        ["status"] = "status",
        ["createdAtUtc"] = "createdAtUtc",
        ["updatedAtUtc"] = "updatedAtUtc"
    };

    public new async Task<ReferenceItem?> GetByIdAsync(string id, CancellationToken cancellationToken)
    {
        var document = await base.GetByIdAsync(id, cancellationToken);
        return document?.ToDomain();
    }

    public new Task<bool> ExistsAsync(string id, CancellationToken cancellationToken) => base.ExistsAsync(id, cancellationToken);

    public Task InsertAsync(ReferenceItem item, CancellationToken cancellationToken) =>
        base.InsertAsync(ReferenceItemDocument.FromDomain(item), cancellationToken);

    public Task ReplaceAsync(ReferenceItem item, CancellationToken cancellationToken) =>
        base.ReplaceAsync(ReferenceItemDocument.FromDomain(item), cancellationToken);

    public new Task DeleteAsync(string id, CancellationToken cancellationToken) => base.DeleteAsync(id, cancellationToken);

    public async Task<ReferenceItemQueryResult> ListAsync(ReferenceItemReadCriteria criteria, CancellationToken cancellationToken)
    {
        var filter = BuildFilter(criteria.Filters);
        var sort = BuildSort(criteria.Sort);

        var totalCount = await Collection.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
        var documents = await Collection
            .Find(filter)
            .Sort(sort)
            .Skip((criteria.Page - 1) * criteria.PageSize)
            .Limit(criteria.PageSize)
            .ToListAsync(cancellationToken);

        return new ReferenceItemQueryResult(documents.Select(document => document.ToDomain()).ToArray(), totalCount);
    }

    private static FilterDefinition<ReferenceItemDocument> BuildFilter(IReadOnlyList<FilterDescriptor> filters)
    {
        if (filters.Count == 0)
        {
            return Builders<ReferenceItemDocument>.Filter.Empty;
        }

        var builder = Builders<ReferenceItemDocument>.Filter;
        var filterDefinitions = new List<FilterDefinition<ReferenceItemDocument>>();

        foreach (var filter in filters)
        {
            filterDefinitions.Add(BuildSingleFilter(builder, filter));
        }

        return builder.And(filterDefinitions);
    }

    private static FilterDefinition<ReferenceItemDocument> BuildSingleFilter(
        FilterDefinitionBuilder<ReferenceItemDocument> builder,
        FilterDescriptor filter)
    {
        var value = filter.Values[0];

        return filter.Field.ToLowerInvariant() switch
        {
            "id" => BuildStringFilter(builder, "_id", filter.Operator, filter.Values),
            "workspaceid" => BuildStringFilter(builder, "workspaceId", filter.Operator, filter.Values),
            "name" => BuildStringFilter(builder, "name", filter.Operator, filter.Values),
            "status" => BuildStringFilter(builder, "status", filter.Operator, filter.Values),
            "tag" => BuildTagFilter(builder, filter.Operator, filter.Values),
            "createdatutc" => BuildDateFilter(builder, "createdAtUtc", filter.Operator, value),
            _ => throw new InvalidOperationException($"Unsupported filter field '{filter.Field}'.")
        };
    }

    private static FilterDefinition<ReferenceItemDocument> BuildStringFilter(
        FilterDefinitionBuilder<ReferenceItemDocument> builder,
        string field,
        FilterOperator filterOperator,
        IReadOnlyList<string> values)
    {
        return filterOperator switch
        {
            FilterOperator.Eq => builder.Eq(field, values[0]),
            FilterOperator.Ne => builder.Ne(field, values[0]),
            FilterOperator.Contains => builder.Regex(field, new BsonRegularExpression(values[0], "i")),
            FilterOperator.StartsWith => builder.Regex(field, new BsonRegularExpression($"^{RegexEscape(values[0])}", "i")),
            FilterOperator.In => builder.In(field, values),
            _ => throw new InvalidOperationException($"Unsupported string operator '{filterOperator}'.")
        };
    }

    private static FilterDefinition<ReferenceItemDocument> BuildTagFilter(
        FilterDefinitionBuilder<ReferenceItemDocument> builder,
        FilterOperator filterOperator,
        IReadOnlyList<string> values)
    {
        return filterOperator switch
        {
            FilterOperator.Eq => builder.AnyEq(document => document.Tags, values[0]),
            FilterOperator.In => builder.AnyIn(document => document.Tags, values),
            _ => throw new InvalidOperationException($"Unsupported tag operator '{filterOperator}'.")
        };
    }

    private static FilterDefinition<ReferenceItemDocument> BuildDateFilter(
        FilterDefinitionBuilder<ReferenceItemDocument> builder,
        string field,
        FilterOperator filterOperator,
        string value)
    {
        var parsed = DateTimeOffset.Parse(value);

        return filterOperator switch
        {
            FilterOperator.Gt => builder.Gt(field, parsed),
            FilterOperator.Gte => builder.Gte(field, parsed),
            FilterOperator.Lt => builder.Lt(field, parsed),
            FilterOperator.Lte => builder.Lte(field, parsed),
            FilterOperator.Eq => builder.Eq(field, parsed),
            _ => throw new InvalidOperationException($"Unsupported date operator '{filterOperator}'.")
        };
    }

    private static SortDefinition<ReferenceItemDocument> BuildSort(IReadOnlyList<SortDescriptor> sorts)
    {
        if (sorts.Count == 0)
        {
            return Builders<ReferenceItemDocument>.Sort.Descending("createdAtUtc");
        }

        var builder = Builders<ReferenceItemDocument>.Sort;
        SortDefinition<ReferenceItemDocument>? current = null;

        foreach (var sort in sorts)
        {
            var field = SortFieldMap[sort.Field];
            var next = sort.Direction == Graphode.BillingEntitlementsService.Contracts.Common.SortDirection.Desc
                ? builder.Descending(field)
                : builder.Ascending(field);

            current = current is null ? next : builder.Combine(current, next);
        }

        return current ?? builder.Descending("createdAtUtc");
    }

    private static string RegexEscape(string value) => System.Text.RegularExpressions.Regex.Escape(value);
}
