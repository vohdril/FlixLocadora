namespace FlixLocadora.Api.Business.Common;

/// <summary>Base of every list query string (?page=1&amp;pageSize=20&amp;search=...). Features extend it with their filters.</summary>
public class ListQuery
{
    public const int MaxPageSize = 100;

    /// <summary>1-based page number.</summary>
    public int Page { get; init; } = 1;

    /// <summary>Items per page (1 to 100).</summary>
    public int PageSize { get; init; } = 20;

    /// <summary>Free-text filter; each resource decides which fields it searches.</summary>
    public string? Search { get; init; }
}

/// <summary>A page of results (the page number is 1-based).</summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int Total)
{
    public bool HasNextPage => Page * PageSize < Total;
}
