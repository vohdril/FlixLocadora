// Created by CrudGen — this file is yours: CrudGen never overwrites it.
using System.Net;
using FlixLocadora.Api.Business.Common;
using FlixLocadora.Api.Business.Movies;
using FlixLocadora.Tests.Support;

namespace FlixLocadora.Tests.Api;

/// <summary>Movies: the CrudGen smoke tests and the stock of copies.</summary>
[Collection(ApiCollection.Name)]
public class MoviesApiTests(ApiFixture fixture)
{
    private readonly HttpClient _client = fixture.CreateClient();

    [Fact]
    public async Task Create_puts_every_copy_in_stock()
    {
        var movie = await _client.NewMovieAsync(copies: 3);

        Assert.Equal((3, 3, true), (movie.TotalCopies, movie.AvailableCopies, movie.IsActive));
    }

    [Fact]
    public async Task The_stock_cannot_go_below_the_rented_copies()
    {
        var movie = await _client.NewMovieAsync(copies: 2);
        await _client.RentAsync(movie.Id, (await _client.NewCustomerAsync()).Id);
        await _client.RentAsync(movie.Id, (await _client.NewCustomerAsync()).Id);

        var below = await _client.PatchEnvelopeAsync<MovieResponse>($"/api/movies/{movie.Id}/copies", new UpdateMovieCopiesRequest(1));
        var above = await _client.PatchEnvelopeAsync<MovieResponse>($"/api/movies/{movie.Id}/copies", new UpdateMovieCopiesRequest(5));

        Assert.Equal((HttpStatusCode.UnprocessableEntity, MovieErrors.CopiesInUse.Code), (below.Status, below.Error.Code));
        Assert.Equal((5, 3), (above.Data.TotalCopies, above.Data.AvailableCopies));
    }

    [Fact]
    public async Task A_movie_with_rental_history_cannot_be_deleted()
    {
        var movie = await _client.NewMovieAsync();
        var rental = (await _client.RentAsync(movie.Id, (await _client.NewCustomerAsync()).Id)).Data;
        await _client.ReturnAsync(rental.Id);

        var reply = await _client.DeleteEnvelopeAsync($"/api/movies/{movie.Id}");

        Assert.Equal((HttpStatusCode.Conflict, MovieErrors.HasRentals.Code), (reply.Status, reply.Error.Code));
    }

    [Fact]
    public async Task The_catalog_filters_by_genre_and_availability()
    {
        var genre = $"G{Guid.NewGuid():N}"[..20];
        var available = await _client.NewMovieAsync(copies: 1, genre: genre);
        await _client.NewMovieAsync(copies: 0, genre: genre);

        var page = (await _client.GetEnvelopeAsync<PagedResult<MovieResponse>>($"/api/movies?genre={genre}&available=true")).Data;

        Assert.Equal(new[] { available.Id }, page.Items.Select(m => m.Id));
    }

    [Fact]
    public async Task List_answers_200_with_a_page()
    {
        using var client = fixture.CreateClient();

        var reply = await client.GetEnvelopeAsync<PagedResult<MovieResponse>>("/api/movies?pageSize=5");

        Assert.Equal(HttpStatusCode.OK, reply.Status);
    }

    [Fact]
    public async Task Get_of_an_unknown_id_answers_404()
    {
        using var client = fixture.CreateClient();

        var reply = await client.GetEnvelopeAsync<MovieResponse>($"/api/movies/987654321");

        Assert.Equal(HttpStatusCode.NotFound, reply.Status);
    }
}
