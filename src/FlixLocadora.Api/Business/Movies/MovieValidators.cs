// Created by CrudGen — this file is yours: CrudGen never overwrites it.
using FlixLocadora.Api.Business.Common;

namespace FlixLocadora.Api.Business.Movies;

public sealed class CreateMovieRequestValidator : AValidatorBase<CreateMovieRequest>
{
    public CreateMovieRequestValidator()
    {
        Include(new CreateMovieRequestDatabaseRules());   // regenerated from the database by CrudGen
        // Business rules of the request (formats, ranges, cross-field rules) go here.
    }
}

public sealed class UpdateMovieRequestValidator : AValidatorBase<UpdateMovieRequest>
{
    public UpdateMovieRequestValidator()
    {
        Include(new UpdateMovieRequestDatabaseRules());
    }
}

public sealed class MovieListQueryValidator : ListQueryValidator<MovieListQuery>;
