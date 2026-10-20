// Created by CrudGen — this file is yours: CrudGen never overwrites it.
using FlixLocadora.Api.Business.Common;

namespace FlixLocadora.Api.Business.Customers;

public sealed class CreateCustomerRequestValidator : AValidatorBase<CreateCustomerRequest>
{
    public CreateCustomerRequestValidator()
    {
        Include(new CreateCustomerRequestDatabaseRules());   // regenerated from the database by CrudGen
        // Business rules of the request (formats, ranges, cross-field rules) go here.
    }
}

public sealed class UpdateCustomerRequestValidator : AValidatorBase<UpdateCustomerRequest>
{
    public UpdateCustomerRequestValidator()
    {
        Include(new UpdateCustomerRequestDatabaseRules());
    }
}

public sealed class CustomerListQueryValidator : ListQueryValidator<CustomerListQuery>;
