using System.ComponentModel.DataAnnotations;

namespace Graphode.BillingEntitlementsService.Contracts.Common;

public enum SortDirection
{
    Asc = 0,
    Desc = 1
}

public enum FilterOperator
{
    Eq = 0,
    Ne = 1,
    Contains = 2,
    StartsWith = 3,
    In = 4,
    Gt = 5,
    Gte = 6,
    Lt = 7,
    Lte = 8
}

public sealed record SortDescriptor(
    [property: Required]
    string Field,
    SortDirection Direction = SortDirection.Asc);

public sealed record FilterDescriptor(
    [property: Required]
    string Field,
    FilterOperator Operator,
    [property: MinLength(1)]
    IReadOnlyList<string> Values);
