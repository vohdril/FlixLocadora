using System.Globalization;
using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;
using FlixLocadora.Api.Business.Common;
using FlixLocadora.Api.Business.Customers;
using FlixLocadora.Api.Business.Movies;
using FlixLocadora.Api.Business.Rentals;
using FlixLocadora.Api.Data.Entities;
using FlixLocadora.Tests.Support;

namespace FlixLocadora.Tests.Api;

/// <summary>
/// The rental rules end to end. Time is real (TimeProvider.System): a rental made "days ago" is arranged by moving
/// its dates back in the database (MoveBackAsync); the exact arithmetic is in Business/RentalPricingTests.
/// </summary>
[Collection(ApiCollection.Name)]
public class RentalsTests(ApiFixture fixture)
{
    private readonly HttpClient _client = fixture.CreateClient();

    [Fact]
    public async Task Rent_answers_201_takes_a_copy_and_freezes_the_daily_rate()
    {
        var movie = await _client.NewMovieAsync(copies: 2, dailyRate: 4.50m);
        var customer = await _client.NewCustomerAsync();

        var reply = await _client.RentAsync(movie.Id, customer.Id, days: 3);

        Assert.Equal(HttpStatusCode.Created, reply.Status);
        var rental = reply.Data;
        Assert.Equal(RentalStatus.Active, rental.Status);
        Assert.Equal((movie.Title, customer.Name), (rental.MovieTitle, rental.CustomerName));
        Assert.Equal(rental.RentedAt.AddDays(3), rental.DueAt);
        Assert.Null(rental.TotalAmount);
        Assert.Equal(1, (await _client.GetMovieAsync(movie.Id)).AvailableCopies);

        // A later price change does not touch the open rental.
        await _client.PutEnvelopeAsync<MovieResponse>($"/api/movies/{movie.Id}", ToUpdate(movie) with { DailyRate = 9.90m });
        Assert.Equal(4.50m, (await _client.GetRentalAsync(rental.Id)).DailyRate);
    }

    [Fact]
    public async Task Ten_clerks_rent_the_last_copy_at_once_and_exactly_one_gets_it()
    {
        var movie = await _client.NewMovieAsync(copies: 1);
        var customers = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => _client.NewCustomerAsync()));

        var replies = await Task.WhenAll(customers.Select(c => _client.RentAsync(movie.Id, c.Id)));

        Assert.Single(replies, r => r.Status == HttpStatusCode.Created);
        Assert.All(replies.Where(r => r.Status != HttpStatusCode.Created), r =>
        {
            Assert.Equal(HttpStatusCode.UnprocessableEntity, r.Status);
            Assert.Equal(RentalErrors.MovieUnavailable.Code, r.Error.Code);
        });
        Assert.Equal(0, (await _client.GetMovieAsync(movie.Id)).AvailableCopies);
    }

    [Fact]
    public async Task The_limit_of_open_rentals_holds_when_the_requests_arrive_together()
    {
        var movie = await _client.NewMovieAsync(copies: 10);
        var customer = await _client.NewCustomerAsync();

        var replies = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => _client.RentAsync(movie.Id, customer.Id)));

        Assert.Equal(3, replies.Count(r => r.Status == HttpStatusCode.Created));       // Rental:MaxActivePerCustomer
        Assert.All(replies.Where(r => r.Status != HttpStatusCode.Created), r => Assert.Equal(RentalErrors.CustomerRentalLimitReached.Code, r.Error.Code));
        Assert.Equal(7, (await _client.GetMovieAsync(movie.Id)).AvailableCopies);
    }

    [Fact]
    public async Task A_customer_with_an_overdue_rental_cannot_rent()
    {
        var movie = await _client.NewMovieAsync(copies: 2);
        var customer = await _client.NewCustomerAsync();
        var rental = (await _client.RentAsync(movie.Id, customer.Id, days: 1)).Data;
        await MoveBackAsync(rental, TimeSpan.FromDays(2));                                 // was due yesterday

        var reply = await _client.RentAsync(movie.Id, customer.Id, days: 1);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, reply.Status);
        Assert.Equal(RentalErrors.CustomerHasOverdueRentals.Code, reply.Error.Code);
        Assert.Equal(RentalStatus.Overdue, (await _client.GetRentalAsync(rental.Id)).Status);
    }

    [Fact]
    public async Task An_inactive_customer_cannot_rent()
    {
        var movie = await _client.NewMovieAsync();
        var customer = await _client.NewCustomerAsync();
        await _client.PutEnvelopeAsync<CustomerResponse>($"/api/customers/{customer.Id}",
            new UpdateCustomerRequest(customer.Name, customer.Email, customer.Cpf, customer.Phone, IsActive: false));

        var reply = await _client.RentAsync(movie.Id, customer.Id);

        Assert.Equal(RentalErrors.CustomerInactive.Code, reply.Error.Code);
        Assert.Equal(1, (await _client.GetMovieAsync(movie.Id)).AvailableCopies);
    }

    [Fact]
    public async Task Unknown_movie_or_customer_answers_422_pointing_to_the_field()
    {
        var movie = await _client.NewMovieAsync();
        var customer = await _client.NewCustomerAsync();

        var noCustomer = await _client.RentAsync(movie.Id, 987654321);
        var noMovie = await _client.RentAsync(987654321, customer.Id);

        Assert.Equal((HttpStatusCode.UnprocessableEntity, RentalErrors.CustomerNotFound.Code, "customerId"), (noCustomer.Status, noCustomer.Error.Code, noCustomer.Error.Field));
        Assert.Equal((HttpStatusCode.UnprocessableEntity, RentalErrors.MovieNotFound.Code, "movieId"), (noMovie.Status, noMovie.Error.Code, noMovie.Error.Field));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(8)]
    public async Task Days_outside_1_to_max_days_answers_400(int days)
    {
        var reply = await _client.RentAsync(1, 1, days);

        Assert.Equal(HttpStatusCode.BadRequest, reply.Status);
        Assert.Equal((ErrorCodes.ValidationFailed.Code, "days"), (reply.Error.Code, reply.Error.Field));
    }

    [Fact]
    public async Task Return_on_time_charges_the_planned_days_and_puts_the_copy_back()
    {
        var (movie, rental) = await NewRentalAsync(days: 3, dailyRate: 4.00m);

        var reply = await _client.ReturnAsync(rental.Id);

        Assert.Equal(HttpStatusCode.OK, reply.Status);
        Assert.Equal(RentalStatus.Returned, reply.Data.Status);
        Assert.NotNull(reply.Data.ReturnedAt);
        Assert.Equal(0m, reply.Data.LateFee);
        Assert.Equal(12.00m, reply.Data.TotalAmount);                                      // 3 days × 4.00, even returned early
        Assert.Equal(1, (await _client.GetMovieAsync(movie.Id)).AvailableCopies);
    }

    [Theory]
    [InlineData(12, 1)]        // 12 hours late → 1 day
    [InlineData(73, 4)]        // 3 days and 1 hour late → 4 days
    public async Task A_late_return_charges_each_started_day_at_the_multiplier(int hoursLate, int lateDays)
    {
        var (_, rental) = await NewRentalAsync(days: 2, dailyRate: 4.00m);
        await MoveBackAsync(rental, TimeSpan.FromDays(2) + TimeSpan.FromHours(hoursLate));

        var returned = (await _client.ReturnAsync(rental.Id)).Data;

        Assert.Equal(lateDays * 4.00m * 1.5m, returned.LateFee);                          // Rental:LateFeeMultiplier
        Assert.Equal(2 * 4.00m + returned.LateFee, returned.TotalAmount);
    }

    [Fact]
    public async Task A_returned_rental_cannot_be_returned_extended_or_cancelled()
    {
        var (_, rental) = await NewRentalAsync(days: 2);
        await _client.ReturnAsync(rental.Id);

        var again = await _client.ReturnAsync(rental.Id);
        var extend = await ExtendAsync(rental.Id, rental.DueAt.AddDays(1));
        var cancel = await _client.DeleteEnvelopeAsync($"/api/rentals/{rental.Id}");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, again.Status);
        Assert.All([again.Error.Code, extend.Error.Code, cancel.Error.Code], code => Assert.Equal(RentalErrors.AlreadyReturned.Code, code));
    }

    [Fact]
    public async Task A_double_click_on_return_puts_the_copy_back_only_once()
    {
        var movie = await _client.NewMovieAsync(copies: 3);
        var rental = (await _client.RentAsync(movie.Id, (await _client.NewCustomerAsync()).Id)).Data;
        await _client.RentAsync(movie.Id, (await _client.NewCustomerAsync()).Id);          // another copy stays rented

        var replies = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => _client.ReturnAsync(rental.Id)));

        Assert.Single(replies, r => r.Status == HttpStatusCode.OK);
        Assert.All(replies.Where(r => r.Status != HttpStatusCode.OK), r => Assert.Equal(RentalErrors.AlreadyReturned.Code, r.Error.Code));
        Assert.Equal(2, (await _client.GetMovieAsync(movie.Id)).AvailableCopies);            // not 3: the other copy is still out
    }

    [Fact]
    public async Task Extend_moves_the_due_date_up_to_the_maximum_period_and_is_paid()
    {
        var (_, rental) = await NewRentalAsync(days: 2, dailyRate: 4.00m);

        var extended = await ExtendAsync(rental.Id, rental.RentedAt.AddDays(5));
        var tooLong = await ExtendAsync(rental.Id, rental.RentedAt.AddDays(7).AddSeconds(1));   // Rental:MaxDays
        var earlier = await ExtendAsync(rental.Id, rental.RentedAt.AddDays(4));

        Assert.Equal(HttpStatusCode.OK, extended.Status);
        Assert.Equal(rental.RentedAt.AddDays(5), extended.Data.DueAt);
        Assert.Equal((HttpStatusCode.UnprocessableEntity, RentalErrors.MaxDaysExceeded.Code, "dueAt"), (tooLong.Status, tooLong.Error.Code, tooLong.Error.Field));
        Assert.Equal(RentalErrors.DueDateNotLater.Code, earlier.Error.Code);
        Assert.Equal(5 * 4.00m, (await _client.ReturnAsync(rental.Id)).Data.TotalAmount);
    }

    [Fact]
    public async Task An_overdue_rental_cannot_be_extended_to_dodge_the_late_fee()
    {
        var (_, rental) = await NewRentalAsync(days: 1);
        await MoveBackAsync(rental, TimeSpan.FromDays(2));

        var reply = await ExtendAsync(rental.Id, DateTime.UtcNow.AddDays(1));

        Assert.Equal((HttpStatusCode.UnprocessableEntity, RentalErrors.Overdue.Code), (reply.Status, reply.Error.Code));
    }

    [Fact]
    public async Task Cancel_right_after_renting_deletes_the_rental_and_puts_the_copy_back()
    {
        var (movie, rental) = await NewRentalAsync();

        var reply = await _client.DeleteEnvelopeAsync($"/api/rentals/{rental.Id}");

        Assert.Equal(HttpStatusCode.OK, reply.Status);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetEnvelopeAsync<RentalResponse>($"/api/rentals/{rental.Id}")).Status);
        Assert.Equal(1, (await _client.GetMovieAsync(movie.Id)).AvailableCopies);
    }

    [Fact]
    public async Task Cancel_after_the_window_answers_422_and_keeps_the_rental()
    {
        var (movie, rental) = await NewRentalAsync(days: 3);
        await MoveBackAsync(rental, TimeSpan.FromHours(1));                                  // Rental:CancelWindowMinutes = 30

        var reply = await _client.DeleteEnvelopeAsync($"/api/rentals/{rental.Id}");

        Assert.Equal((HttpStatusCode.UnprocessableEntity, RentalErrors.CancelWindowExpired.Code), (reply.Status, reply.Error.Code));
        Assert.Equal(0, (await _client.GetMovieAsync(movie.Id)).AvailableCopies);
    }

    [Fact]
    public async Task Lists_filter_by_the_derived_status_and_the_history_belongs_to_the_customer()
    {
        var movie = await _client.NewMovieAsync(copies: 3);
        var customer = await _client.NewCustomerAsync();
        var overdue = (await _client.RentAsync(movie.Id, customer.Id, days: 1)).Data;
        var returned = (await _client.RentAsync(movie.Id, customer.Id, days: 1)).Data;
        await _client.ReturnAsync(returned.Id);
        await MoveBackAsync(overdue, TimeSpan.FromDays(3));
        await _client.RentAsync(movie.Id, (await _client.NewCustomerAsync()).Id);           // someone else's rental

        var overdueOnly = await _client.GetEnvelopeAsync<PagedResult<RentalResponse>>($"/api/rentals?status=Overdue&customerId={customer.Id}");
        var history = await _client.GetEnvelopeAsync<PagedResult<RentalResponse>>($"/api/customers/{customer.Id}/rentals");
        var unknown = await _client.GetEnvelopeAsync<PagedResult<RentalResponse>>("/api/customers/987654321/rentals");

        Assert.Equal(new[] { overdue.Id }, overdueOnly.Data.Items.Select(r => r.Id));
        Assert.Equal(new[] { (returned.Id, RentalStatus.Returned), (overdue.Id, RentalStatus.Overdue) }, history.Data.Items.Select(r => (r.Id, r.Status)));
        Assert.Equal(HttpStatusCode.NotFound, unknown.Status);
    }

    [Fact]
    public async Task Dates_are_utc_both_ways()
    {
        var (_, rental) = await NewRentalAsync(days: 2);
        var dueAt = rental.RentedAt.AddDays(3);
        var brasilia = new DateTimeOffset(dueAt).ToOffset(TimeSpan.FromHours(-3)).ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture);

        var extended = await Reply<RentalResponse>.From(await _client.PutAsync($"/api/rentals/{rental.Id}",
            new StringContent($$"""{"dueAt":"{{brasilia}}"}""", Encoding.UTF8, "application/json")));
        await _client.ReturnAsync(rental.Id);
        var json = await _client.GetStringAsync($"/api/rentals/{rental.Id}");

        Assert.Equal(dueAt, extended.Data.DueAt);                         // -03:00 in, UTC kept
        Assert.Matches("\"rentedAt\":\"[0-9T:-]+Z\"", json);            // datetime2 out with the Z
        Assert.Matches("\"dueAt\":\"[0-9T:-]+Z\"", json);
        Assert.Matches("\"returnedAt\":\"[0-9T:-]+Z\"", json);          // nullable too
    }

    private async Task<(MovieResponse Movie, RentalResponse Rental)> NewRentalAsync(int days = 2, decimal dailyRate = 4.00m)
    {
        var movie = await _client.NewMovieAsync(copies: 1, dailyRate);
        var customer = await _client.NewCustomerAsync();
        return (movie, (await _client.RentAsync(movie.Id, customer.Id, days)).Data);
    }

    private Task<Reply<RentalResponse>> ExtendAsync(int id, DateTime dueAt)
        => _client.PutEnvelopeAsync<RentalResponse>($"/api/rentals/{id}", new UpdateRentalRequest(dueAt));

    /// <summary>"This rental was made <paramref name="back"/> ago": moves both dates, keeping the period.</summary>
    private Task MoveBackAsync(RentalResponse rental, TimeSpan back)
        => fixture.ArrangeAsync(db => db.Set<Rental>().Where(r => r.Id == rental.Id).ExecuteUpdateAsync(s => s
            .SetProperty(r => r.RentedAt, rental.RentedAt - back)
            .SetProperty(r => r.DueAt, rental.DueAt - back)));

    private static UpdateMovieRequest ToUpdate(MovieResponse m) => new(m.Title, m.Genre, m.ReleaseYear, m.Rating, m.PosterUrl, m.DailyRate, m.IsActive);
}
