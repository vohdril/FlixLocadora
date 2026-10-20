using FluentValidation;

namespace FlixLocadora.Api.Business.Common;

/// <summary>
/// Base for every validator: binds each rule to a TYPED <see cref="ErrorCode"/> (Foundation.API pattern).
/// Validators are PURE — no I/O. Rules that need the database (uniqueness, existence) belong to the services,
/// and the final guarantee belongs to the database constraints.
/// </summary>
public abstract class AValidatorBase<T> : AbstractValidator<T>
{
    protected static IRuleBuilderOptions<T, TProperty> WithCode<TProperty>(IRuleBuilderOptions<T, TProperty> rule, ErrorCode code)
        => rule.WithErrorCode(code.Code).WithMessage(code.DefaultMessage);
}

/// <summary>Parameters level: the paging rules shared by every list query.</summary>
public abstract class ListQueryValidator<TQuery> : AValidatorBase<TQuery>
    where TQuery : ListQuery
{
    protected ListQueryValidator()
    {
        WithCode(RuleFor(q => q.Page).GreaterThanOrEqualTo(1), ErrorCodes.InvalidPagination);
        WithCode(RuleFor(q => q.PageSize).InclusiveBetween(1, ListQuery.MaxPageSize), ErrorCodes.InvalidPagination);
        RuleFor(q => q.Search).MaximumLength(100);
    }
}

/// <summary>Validator of the plain <see cref="ListQuery"/> (resources without extra filters).</summary>
public sealed class DefaultListQueryValidator : ListQueryValidator<ListQuery>;
