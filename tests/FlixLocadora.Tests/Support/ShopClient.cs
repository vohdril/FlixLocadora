using FlixLocadora.Api.Business.Customers;
using FlixLocadora.Api.Business.Movies;
using FlixLocadora.Api.Business.Rentals;

namespace FlixLocadora.Tests.Support;

/// <summary>The shop arranged over HTTP — the same calls a front end makes.</summary>
public static class ShopClient
{
    public static async Task<MovieResponse> NewMovieAsync(this HttpClient client, int copies = 1, decimal dailyRate = 4.00m, string genre = "Drama")
        => (await client.PostEnvelopeAsync<MovieResponse>("/api/movies", TestData.NewMovie(copies, dailyRate, genre))).Data;

    public static async Task<CustomerResponse> NewCustomerAsync(this HttpClient client)
        => (await client.PostEnvelopeAsync<CustomerResponse>("/api/customers", TestData.NewCustomer())).Data;

    public static Task<Reply<RentalResponse>> RentAsync(this HttpClient client, int movieId, int customerId, int days = 2)
        => client.PostEnvelopeAsync<RentalResponse>("/api/rentals", new CreateRentalRequest(movieId, customerId, days));

    public static Task<Reply<RentalResponse>> ReturnAsync(this HttpClient client, int rentalId)
        => client.PostEnvelopeAsync<RentalResponse>($"/api/rentals/{rentalId}/return");

    public static async Task<MovieResponse> GetMovieAsync(this HttpClient client, int id)
        => (await client.GetEnvelopeAsync<MovieResponse>($"/api/movies/{id}")).Data;

    public static async Task<RentalResponse> GetRentalAsync(this HttpClient client, int id)
        => (await client.GetEnvelopeAsync<RentalResponse>($"/api/rentals/{id}")).Data;
}
