using FlixLocadora.Api.Data.Entities;

namespace FlixLocadora.Api.Business.Rentals;

/// <summary>
/// The money and the dates of a rental — PURE (time comes in as a parameter), so it is tested without a database.
///   total    = planned days × daily rate (an early return is not refunded) + late fee
///   late fee = days late × daily rate × Rental:LateFeeMultiplier; a started day counts as a whole day
/// The daily rate is frozen when the movie is rented: a later price change does not touch open rentals.
/// </summary>
public static class RentalPricing
{
    public static Rental Open(int movieId, int customerId, decimal dailyRate, int days, DateTime now)
    {
        var rentedAt = Stored(now);
        return new Rental
        {
            MovieId = movieId,
            CustomerId = customerId,
            RentedAt = rentedAt,
            DueAt = rentedAt.AddDays(days),
            DailyRate = dailyRate
        };
    }

    public static void Close(Rental rental, DateTime now, decimal lateFeeMultiplier)
    {
        var returnedAt = Stored(now) < rental.RentedAt ? rental.RentedAt : Stored(now);

        rental.ReturnedAt = returnedAt;
        rental.LateFee = Money(LateDays(rental, returnedAt) * rental.DailyRate * lateFeeMultiplier);
        rental.TotalAmount = Money(PlannedDays(rental) * rental.DailyRate) + rental.LateFee;
    }

    public static RentalStatus StatusOf(Rental rental, DateTime now)
        => rental.ReturnedAt is not null ? RentalStatus.Returned
            : now > rental.DueAt ? RentalStatus.Overdue
            : RentalStatus.Active;

    /// <summary>Days paid in any case: from the rental to the due date (an extension adds to them).</summary>
    public static int PlannedDays(Rental rental) => Math.Max(1, WholeDays(rental.DueAt - rental.RentedAt));

    /// <summary>Days after the due date, rounded up: 3 days and 1 hour late are 4 days.</summary>
    public static int LateDays(Rental rental, DateTime returnedAt) => returnedAt > rental.DueAt ? WholeDays(returnedAt - rental.DueAt) : 0;

    /// <summary>
    /// UTC with whole seconds — what a datetime2(0) column keeps, so what is computed is what is stored.
    /// A value without kind is taken as UTC (the API contract); a local one is converted.
    /// </summary>
    public static DateTime Stored(DateTime value)
    {
        var utc = value.Kind == DateTimeKind.Local ? value.ToUniversalTime() : value;
        return new DateTime(utc.Ticks - utc.Ticks % TimeSpan.TicksPerSecond, DateTimeKind.Utc);
    }

    // The tolerance keeps an exact number of days (e.g. 3.0000000001 from double arithmetic) from becoming one more.
    private static int WholeDays(TimeSpan span) => (int)Math.Ceiling(span.TotalDays - 1e-9);

    private static decimal Money(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
