// Created by CrudGen — this file is yours: CrudGen never overwrites it.
using FluentValidation;
using FlixLocadora.Api.Business.Common;

namespace FlixLocadora.Api.Business.Movies;

public sealed class CreateMovieRequestValidator : AValidatorBase<CreateMovieRequest>
{
    public CreateMovieRequestValidator()
    {
        Include(new CreateMovieRequestDatabaseRules());   // regenerated from the database by CrudGen
        RuleFor(x => x.ReleaseYear).InclusiveBetween((short)1888, (short)2100);
        RuleFor(x => x.Rating).InclusiveBetween((byte)1, (byte)5);
        RuleFor(x => x.PosterUrl).Must(MovieRules.BeAnAbsoluteUrl).WithMessage("PosterUrl must be an absolute http(s) URL.");
        RuleFor(x => x.DailyRate).GreaterThanOrEqualTo(0);
        RuleFor(x => x.TotalCopies).InclusiveBetween(0, 1000);
    }
}

public sealed class UpdateMovieRequestValidator : AValidatorBase<UpdateMovieRequest>
{
    public UpdateMovieRequestValidator()
    {
        Include(new UpdateMovieRequestDatabaseRules());
        RuleFor(x => x.ReleaseYear).InclusiveBetween((short)1888, (short)2100);
        RuleFor(x => x.Rating).InclusiveBetween((byte)1, (byte)5);
        RuleFor(x => x.PosterUrl).Must(MovieRules.BeAnAbsoluteUrl).WithMessage("PosterUrl must be an absolute http(s) URL.");
        RuleFor(x => x.DailyRate).GreaterThanOrEqualTo(0);
    }
}

public sealed class UpdateMovieCopiesRequestValidator : AValidatorBase<UpdateMovieCopiesRequest>
{
    public UpdateMovieCopiesRequestValidator() => RuleFor(x => x.TotalCopies).InclusiveBetween(0, 1000);
}

public sealed class MovieListQueryValidator : ListQueryValidator<MovieListQuery>
{
    public MovieListQueryValidator() => RuleFor(q => q.Genre).MaximumLength(50);
}

internal static class MovieRules
{
    public static bool BeAnAbsoluteUrl(string? url)
        => url is null || (Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https");
}
