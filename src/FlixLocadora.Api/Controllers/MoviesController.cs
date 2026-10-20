// Created by CrudGen — this file is yours: CrudGen never overwrites it.
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FlixLocadora.Api.Business.Common;
using FlixLocadora.Api.Business.Movies;

namespace FlixLocadora.Api.Controllers;

/// <summary>Movies (dbo.Movie).</summary>
[Route("api/movies")]
[Authorize(Policy = Policies.Read)]
[AllowAnonymous] // TODO(auth go-live): remove this line to enforce the policies
public sealed class MoviesController(IMovieService service)
    : CrudController<int, MovieListQuery, CreateMovieRequest, UpdateMovieRequest, MovieResponse>(service);
