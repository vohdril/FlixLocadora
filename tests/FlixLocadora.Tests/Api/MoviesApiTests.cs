// Created by CrudGen — this file is yours: CrudGen never overwrites it.
using System.Net;
using FlixLocadora.Api.Business.Common;
using FlixLocadora.Api.Business.Movies;
using FlixLocadora.Tests.Support;

namespace FlixLocadora.Tests.Api;

/// <summary>Smoke tests created by CrudGen — the real scenarios of Movies go here.</summary>
[Collection(ApiCollection.Name)]
public class MoviesApiTests(ApiFixture fixture)
{
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
