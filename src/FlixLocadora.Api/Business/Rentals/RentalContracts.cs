using FlixLocadora.Api.Business.Common;

namespace FlixLocadora.Api.Business.Rentals;

/// <summary>DERIVED from the dates, never stored (storing it would need a job to turn rentals "overdue" at midnight).</summary>
public enum RentalStatus
{
    Active,
    Overdue,
    Returned
}

/// <summary>Rents one copy of a movie.</summary>
/// <param name="MovieId">An active movie with a copy available.</param>
/// <param name="CustomerId">An active customer without overdue rentals and under the limit of open rentals.</param>
/// <param name="Days">Rental period, from 1 to Rental:MaxDays (7 by default).</param>
public sealed record CreateRentalRequest(int MovieId, int CustomerId, int Days);

/// <summary>Extends an open rental that is not overdue.</summary>
/// <param name="DueAt">New due date and time, in UTC: later than the current one and at most Rental:MaxDays after the rental.</param>
public sealed record UpdateRentalRequest(DateTime DueAt);

/// <summary>A rental. LateFee and TotalAmount are filled when the copy is returned.</summary>
public sealed record RentalResponse(
    int Id,
    int MovieId,
    string MovieTitle,
    int CustomerId,
    string CustomerName,
    DateTime RentedAt,
    DateTime DueAt,
    DateTime? ReturnedAt,
    decimal DailyRate,
    decimal? LateFee,
    decimal? TotalAmount,
    RentalStatus Status);

/// <summary>Filters of GET /api/rentals. Search matches the movie title or the customer name.</summary>
public sealed class RentalListQuery : ListQuery
{
    public RentalStatus? Status { get; init; }

    public int? CustomerId { get; init; }

    public int? MovieId { get; init; }
}

/// <summary>Rules of the shop (section "Rental"), validated at startup.</summary>
public sealed class RentalOptions
{
    public const string Section = "Rental";

    public int MaxActivePerCustomer { get; set; } = 3;

    public int MaxDays { get; set; } = 7;

    /// <summary>Late fee per day = daily rate × multiplier.</summary>
    public decimal LateFeeMultiplier { get; set; } = 1.5m;

    /// <summary>A rental can be cancelled (registered by mistake) only this long after it was made; afterwards the copy is returned — and paid.</summary>
    public int CancelWindowMinutes { get; set; } = 30;
}

public static class RentalErrors
{
    public static readonly ErrorCode MovieNotFound = new("RENTAL_MOVIE_NOT_FOUND", "The movie does not exist.", ErrorKind.BusinessRule);
    public static readonly ErrorCode MovieUnavailable = new("MOVIE_UNAVAILABLE", "No copy of this movie is available right now.", ErrorKind.BusinessRule);
    public static readonly ErrorCode CustomerNotFound = new("RENTAL_CUSTOMER_NOT_FOUND", "The customer does not exist.", ErrorKind.BusinessRule);
    public static readonly ErrorCode CustomerInactive = new("CUSTOMER_INACTIVE", "The customer is inactive.", ErrorKind.BusinessRule);
    public static readonly ErrorCode CustomerHasOverdueRentals = new("CUSTOMER_HAS_OVERDUE_RENTALS", "The customer has overdue rentals: they must be returned first.", ErrorKind.BusinessRule);
    public static readonly ErrorCode CustomerRentalLimitReached = new("CUSTOMER_RENTAL_LIMIT_REACHED", "The customer already has the maximum number of open rentals.", ErrorKind.BusinessRule);
    public static readonly ErrorCode AlreadyReturned = new("RENTAL_ALREADY_RETURNED", "The rental was already returned.", ErrorKind.BusinessRule);
    public static readonly ErrorCode Overdue = new("RENTAL_OVERDUE", "The rental is overdue: it must be returned (with the late fee), not extended.", ErrorKind.BusinessRule);
    public static readonly ErrorCode DueDateNotLater = new("RENTAL_DUE_DATE_NOT_LATER", "The new due date must be later than the current one.", ErrorKind.BusinessRule);
    public static readonly ErrorCode MaxDaysExceeded = new("RENTAL_MAX_DAYS_EXCEEDED", "The rental period exceeds the maximum allowed.", ErrorKind.BusinessRule);
    public static readonly ErrorCode CancelWindowExpired = new("RENTAL_CANCEL_WINDOW_EXPIRED", "The rental can no longer be cancelled: the copy must be returned.", ErrorKind.BusinessRule);
}

/// <summary>
/// Foreign keys of dbo.Rental on INSERT → typed errors. Defensive: the service locks the customer and reserves the
/// copy first, so a concurrent delete waits and fails on its own side (MOVIE_HAS_RENTALS / CUSTOMER_HAS_RENTALS).
/// </summary>
public sealed class RentalConstraints : ConstraintErrorCatalog
{
    public RentalConstraints()
    {
        Map("FK_Rental_Movie", RentalErrors.MovieNotFound, field: "movieId", statement: "INSERT");
        Map("FK_Rental_Customer", RentalErrors.CustomerNotFound, field: "customerId", statement: "INSERT");
    }
}
