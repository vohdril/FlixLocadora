// Created by CrudGen — this file is yours: CrudGen never overwrites it.
using Riok.Mapperly.Abstractions;
using FlixLocadora.Api.Business.Common;
using FlixLocadora.Api.Data.Entities;

namespace FlixLocadora.Api.Business.Movies;

/// <summary>Movie ⇄ contracts (Mapperly: a member without a counterpart is a build error).</summary>
[Mapper(AutoUserMappings = false)]
public sealed partial class MovieConverter : IEntityConverter<Movie, CreateMovieRequest, UpdateMovieRequest, MovieResponse>
{
    [MapperIgnoreTarget(nameof(Movie.Id))]
    [MapperIgnoreTarget(nameof(Movie.AvailableCopies))]
    [MapperIgnoreTarget(nameof(Movie.IsActive))]
    [MapperIgnoreTarget(nameof(Movie.CreatedAt))]
    [MapperIgnoreTarget(nameof(Movie.Rentals))]
    public partial Movie ToEntity(CreateMovieRequest request);

    [MapperIgnoreTarget(nameof(Movie.Id))]
    [MapperIgnoreTarget(nameof(Movie.TotalCopies))]
    [MapperIgnoreTarget(nameof(Movie.AvailableCopies))]
    [MapperIgnoreTarget(nameof(Movie.CreatedAt))]
    [MapperIgnoreTarget(nameof(Movie.Rentals))]
    public partial void Apply(UpdateMovieRequest request, [MappingTarget] Movie entity);

    [MapperIgnoreSource(nameof(Movie.Rentals))]
    public partial MovieResponse ToResponse(Movie entity);
}
