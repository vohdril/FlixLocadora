// Created by CrudGen — this file is yours: CrudGen never overwrites it.
using Microsoft.EntityFrameworkCore;
using FlixLocadora.Api.Business.Common;
using FlixLocadora.Api.Data;
using FlixLocadora.Api.Data.Entities;
using FlixLocadora.Api.Data.Repositories;

namespace FlixLocadora.Api.Business.Movies;

public interface IMovieService : ICrudService<int, MovieListQuery, CreateMovieRequest, UpdateMovieRequest, MovieResponse>;

/// <summary>
/// Movies — the CRUD comes from CrudService; business rules go in the hooks below
/// (Filter, Order, OnCreating/OnUpdating, Check*). Unique columns are pre-checked for a friendly 409;
/// the database constraints stay the real guarantee (MovieConstraints).
/// </summary>
public sealed class MovieService(
    IRepository<Movie> movies,
    IRepository<Rental> rentals,
    IUnitOfWork unitOfWork,
    IEntityConverter<Movie, CreateMovieRequest, UpdateMovieRequest, MovieResponse> converter)
    : CrudService<Movie, int, MovieListQuery, CreateMovieRequest, UpdateMovieRequest, MovieResponse>(movies, unitOfWork, converter),
      IMovieService
{
    protected override IQueryable<Movie> Filter(IQueryable<Movie> query, MovieListQuery parameters)
    {
        if (string.IsNullOrWhiteSpace(parameters.Search))
            return query;

        var search = parameters.Search.Trim();
        return query.Where(e => e.Title.Contains(search) || e.Genre.Contains(search));
    }

    protected override IOrderedQueryable<Movie> Order(IQueryable<Movie> query) => query.OrderBy(e => e.Title).ThenBy(e => e.Id);

    protected override async Task<ApiResponse?> CheckDeleteAsync(Movie entity, CancellationToken ct)
    {
        if (await rentals.Query().AnyAsync(r => r.MovieId == entity.Id, ct))
            return Responses.Fail(MovieErrors.HasRentals);

        return null;
    }
}
