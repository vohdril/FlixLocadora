// Created by CrudGen — this file is yours: CrudGen never overwrites it.
using Microsoft.EntityFrameworkCore;
using FlixLocadora.Api.Business.Common;
using FlixLocadora.Api.Data;
using FlixLocadora.Api.Data.Entities;
using FlixLocadora.Api.Data.Repositories;

namespace FlixLocadora.Api.Business.Movies;

public interface IMovieService : ICrudService<int, MovieListQuery, CreateMovieRequest, UpdateMovieRequest, MovieResponse>
{
    Task<ApiResponse<MovieResponse>> SetCopiesAsync(int id, UpdateMovieCopiesRequest request, CancellationToken ct);
}

/// <summary>
/// Movies — the CRUD comes from CrudService. Specific: the stock of copies (AvailableCopies = TotalCopies on
/// create; changed afterwards only by rentals and by SetCopiesAsync, both atomic in MovieRepository) and the
/// catalog filters.
/// </summary>
public sealed class MovieService(
    IMovieRepository movies,
    IRepository<Rental> rentals,
    IUnitOfWork unitOfWork,
    IEntityConverter<Movie, CreateMovieRequest, UpdateMovieRequest, MovieResponse> converter)
    : CrudService<Movie, int, MovieListQuery, CreateMovieRequest, UpdateMovieRequest, MovieResponse>(movies, unitOfWork, converter),
      IMovieService
{
    public async Task<ApiResponse<MovieResponse>> SetCopiesAsync(int id, UpdateMovieCopiesRequest request, CancellationToken ct)
    {
        if (!await Repository.Query().AnyAsync(m => m.Id == id, ct))
            return Responses.NotFound<MovieResponse>();

        if (!await Movies.TrySetTotalCopiesAsync(id, request.TotalCopies, ct))
            return Responses.Fail<MovieResponse>(MovieErrors.CopiesInUse, field: "totalCopies");

        return await GetAsync(id, ct);
    }

    private IMovieRepository Movies => (IMovieRepository)Repository;

    protected override void OnCreating(CreateMovieRequest request, Movie entity) => entity.AvailableCopies = entity.TotalCopies;

    protected override IQueryable<Movie> Filter(IQueryable<Movie> query, MovieListQuery parameters)
    {
        if (!string.IsNullOrWhiteSpace(parameters.Search))
        {
            var search = parameters.Search.Trim();
            query = query.Where(e => e.Title.Contains(search) || e.Genre.Contains(search));
        }

        if (!string.IsNullOrWhiteSpace(parameters.Genre))
            query = query.Where(e => e.Genre == parameters.Genre.Trim());

        if (parameters.Available is { } available)
            query = available ? query.Where(e => e.IsActive && e.AvailableCopies > 0) : query.Where(e => !e.IsActive || e.AvailableCopies == 0);

        return query;
    }

    protected override IOrderedQueryable<Movie> Order(IQueryable<Movie> query) => query.OrderBy(e => e.Title).ThenBy(e => e.Id);

    protected override async Task<ApiResponse?> CheckDeleteAsync(Movie entity, CancellationToken ct)
    {
        if (await rentals.Query().AnyAsync(r => r.MovieId == entity.Id, ct))
            return Responses.Fail(MovieErrors.HasRentals);

        return null;
    }
}
