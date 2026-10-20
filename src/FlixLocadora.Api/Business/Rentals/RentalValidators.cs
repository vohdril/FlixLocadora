using FluentValidation;
using Microsoft.Extensions.Options;
using FlixLocadora.Api.Business.Common;

namespace FlixLocadora.Api.Business.Rentals;

// Request level: the SHAPE (400). Rules that need the database (customer, stock, dates of the rental) are in RentalService.

public sealed class CreateRentalRequestValidator : AValidatorBase<CreateRentalRequest>
{
    public CreateRentalRequestValidator(IOptions<RentalOptions> options)
    {
        RuleFor(x => x.MovieId).GreaterThan(0);
        RuleFor(x => x.CustomerId).GreaterThan(0);
        RuleFor(x => x.Days).InclusiveBetween(1, options.Value.MaxDays);
    }
}

public sealed class UpdateRentalRequestValidator : AValidatorBase<UpdateRentalRequest>
{
    public UpdateRentalRequestValidator() => RuleFor(x => x.DueAt).NotEmpty();
}

public sealed class RentalListQueryValidator : ListQueryValidator<RentalListQuery>
{
    public RentalListQueryValidator()
    {
        RuleFor(q => q.Status).IsInEnum();
        RuleFor(q => q.CustomerId).GreaterThan(0);
        RuleFor(q => q.MovieId).GreaterThan(0);
    }
}

/// <summary>Configuration level: the shop rules are checked when the API starts.</summary>
public sealed class RentalOptionsValidator : AValidatorBase<RentalOptions>
{
    public RentalOptionsValidator()
    {
        RuleFor(o => o.MaxActivePerCustomer).InclusiveBetween(1, 20);
        RuleFor(o => o.MaxDays).InclusiveBetween(1, 60);
        RuleFor(o => o.LateFeeMultiplier).InclusiveBetween(0m, 10m);
        RuleFor(o => o.CancelWindowMinutes).InclusiveBetween(0, 1440);
    }
}
