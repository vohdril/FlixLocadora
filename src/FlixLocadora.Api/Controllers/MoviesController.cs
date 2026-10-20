// Created by CrudGen — this file is yours: CrudGen never overwrites it.
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FlixLocadora.Api.Business.Common;
using FlixLocadora.Api.Business.Movies;
using FlixLocadora.Api.Infra.Http;

namespace FlixLocadora.Api.Controllers;

/// <summary>Movies of the shop — catalog and stock of copies.</summary>
[Route("api/movies")]
[Authorize(Policy = Policies.Read)]
[AllowAnonymous] // TODO(auth go-live): remove this line to enforce the policies
public sealed class MoviesController : CrudController<int, MovieListQuery, CreateMovieRequest, UpdateMovieRequest, MovieResponse>
{
    private readonly IMovieService _movies;

    public MoviesController(IMovieService movies) : base(movies) => _movies = movies;

    /// <summary>Sets the stock of copies (never below the copies rented right now).</summary>
    [HttpPatch("{id}/copies")]
    [Authorize(Policy = Policies.Write)]
    public async Task<ActionResult<ApiResponse<MovieResponse>>> SetCopies(int id, UpdateMovieCopiesRequest request, CancellationToken ct)
        => ResponseTreatment.General(await _movies.SetCopiesAsync(id, request, ct));
}
