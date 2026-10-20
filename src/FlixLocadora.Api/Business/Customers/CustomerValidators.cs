// Created by CrudGen — this file is yours: CrudGen never overwrites it.
using FluentValidation;
using FlixLocadora.Api.Business.Common;

namespace FlixLocadora.Api.Business.Customers;

/// <summary>What both customer writes carry — one set of format rules for the two contracts.</summary>
public interface ICustomerData
{
    string Email { get; }
    string Cpf { get; }
    string? Phone { get; }
}

public sealed partial record CreateCustomerRequest : ICustomerData;

public sealed partial record UpdateCustomerRequest : ICustomerData;

public static partial class CustomerErrors
{
    public static readonly ErrorCode CpfInvalid = new("CUSTOMER_CPF_INVALID", "The CPF is not valid (11 digits with valid check digits).", ErrorKind.Validation);
}

internal sealed class CustomerDataRules<T> : AValidatorBase<T>
    where T : ICustomerData
{
    public CustomerDataRules()
    {
        RuleFor(x => x.Email).EmailAddress();
        WithCode(RuleFor(x => x.Cpf).Must(Cpf.IsValid), CustomerErrors.CpfInvalid);
        RuleFor(x => x.Phone)
            .Must(phone => Text.Digits(phone).Length is 10 or 11)
            .WithMessage("Phone must have 10 or 11 digits (area code + number).")
            .When(x => !string.IsNullOrWhiteSpace(x.Phone));
    }
}

public sealed class CreateCustomerRequestValidator : AValidatorBase<CreateCustomerRequest>
{
    public CreateCustomerRequestValidator()
    {
        Include(new CreateCustomerRequestDatabaseRules());   // regenerated from the database by CrudGen
        Include(new CustomerDataRules<CreateCustomerRequest>());
    }
}

public sealed class UpdateCustomerRequestValidator : AValidatorBase<UpdateCustomerRequest>
{
    public UpdateCustomerRequestValidator()
    {
        Include(new UpdateCustomerRequestDatabaseRules());
        Include(new CustomerDataRules<UpdateCustomerRequest>());
    }
}

public sealed class CustomerListQueryValidator : ListQueryValidator<CustomerListQuery>;
