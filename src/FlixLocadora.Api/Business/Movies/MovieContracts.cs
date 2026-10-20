using FlixLocadora.Api.Business.Common;

namespace FlixLocadora.Api.Business.Movies;

// Hand-written half of the movie contracts (Generated/MovieContracts.g.cs follows the database).

/// <summary>Filters of the catalog — in SQL (MyFlix loaded the whole table to filter in memory).</summary>
public sealed partial class MovieListQuery
{
    /// <summary>Exact genre (e.g. Drama).</summary>
    public string? Genre { get; init; }

    /// <summary>true = only movies with a copy to rent right now.</summary>
    public bool? Available { get; init; }
}

/// <summary>New stock of a movie. It cannot go below the copies that are rented right now.</summary>
public sealed record UpdateMovieCopiesRequest(int TotalCopies);

public static partial class MovieErrors
{
    public static readonly ErrorCode CopiesInUse = new("MOVIE_COPIES_IN_USE", "The new total is below the copies rented right now.", ErrorKind.BusinessRule);
}
