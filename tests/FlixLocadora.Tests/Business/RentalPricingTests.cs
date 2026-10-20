using FlixLocadora.Api.Business.Rentals;

namespace FlixLocadora.Tests.Business;

/// <summary>The money of a rental, with exact times — no database, no clock.</summary>
public class RentalPricingTests
{
    private static readonly DateTime Monday = new(2026, 10, 19, 14, 30, 0, DateTimeKind.Utc);

    [Fact]
    public void Open_freezes_the_rate_and_keeps_whole_seconds_in_utc()
    {
        var rental = RentalPricing.Open(movieId: 1, customerId: 2, dailyRate: 4.50m, days: 3, now: Monday.AddTicks(1_234_567));

        Assert.Equal(Monday, rental.RentedAt);
        Assert.Equal(DateTimeKind.Utc, rental.RentedAt.Kind);
        Assert.Equal(Monday.AddDays(3), rental.DueAt);
        Assert.Equal(4.50m, rental.DailyRate);
    }

    [Theory]
    [InlineData(-30, 0)]       // returned early
    [InlineData(0, 0)]         // exactly on the due date
    [InlineData(1, 1)]         // 1 hour late: a started day counts
    [InlineData(24, 1)]        // exactly 1 day
    [InlineData(25, 2)]
    [InlineData(73, 4)]        // 3 days and 1 hour → 4
    public void Late_days_are_rounded_up(int hoursAfterDue, int lateDays)
    {
        var rental = RentalPricing.Open(1, 2, 4.00m, days: 2, Monday);

        Assert.Equal(lateDays, RentalPricing.LateDays(rental, rental.DueAt.AddHours(hoursAfterDue)));
    }

    [Fact]
    public void Close_charges_the_planned_days_plus_the_late_fee()
    {
        var rental = RentalPricing.Open(1, 2, 4.00m, days: 2, Monday);

        RentalPricing.Close(rental, Monday.AddDays(2).AddHours(73), lateFeeMultiplier: 1.5m);

        Assert.Equal(Monday.AddDays(2).AddHours(73), rental.ReturnedAt);
        Assert.Equal(24.00m, rental.LateFee);          // 4 days × 4.00 × 1.5
        Assert.Equal(32.00m, rental.TotalAmount);      // 2 × 4.00 + 24.00
    }

    [Fact]
    public void An_early_return_pays_the_planned_days()
    {
        var rental = RentalPricing.Open(1, 2, 4.00m, days: 5, Monday);

        RentalPricing.Close(rental, Monday.AddHours(3), lateFeeMultiplier: 1.5m);

        Assert.Equal(0m, rental.LateFee);
        Assert.Equal(20.00m, rental.TotalAmount);
    }

    [Fact]
    public void Money_is_rounded_to_cents_half_away_from_zero()
    {
        var rental = RentalPricing.Open(1, 2, 3.33m, days: 1, Monday);

        RentalPricing.Close(rental, Monday.AddDays(1).AddHours(1), lateFeeMultiplier: 1.5m);

        Assert.Equal(5.00m, rental.LateFee);           // 1 day × 3.33 × 1.5 = 4.995
        Assert.Equal(8.33m, rental.TotalAmount);
    }

    [Fact]
    public void Status_is_derived_from_the_dates()
    {
        var rental = RentalPricing.Open(1, 2, 4.00m, days: 2, Monday);

        Assert.Equal(RentalStatus.Active, RentalPricing.StatusOf(rental, rental.DueAt));
        Assert.Equal(RentalStatus.Overdue, RentalPricing.StatusOf(rental, rental.DueAt.AddSeconds(1)));

        RentalPricing.Close(rental, Monday.AddDays(5), 1.5m);
        Assert.Equal(RentalStatus.Returned, RentalPricing.StatusOf(rental, Monday.AddDays(9)));
    }

    [Fact]
    public void Stored_takes_a_value_without_kind_as_utc_and_converts_a_local_one()
    {
        Assert.Equal(Monday, RentalPricing.Stored(DateTime.SpecifyKind(Monday, DateTimeKind.Unspecified)));
        Assert.Equal(Monday, RentalPricing.Stored(Monday.ToLocalTime()));
    }
}
