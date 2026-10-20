// Created by CrudGen — this file is yours: CrudGen never overwrites it.
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FlixLocadora.Api.Business.Common;
using FlixLocadora.Api.Business.Customers;

namespace FlixLocadora.Api.Controllers;

/// <summary>Customers (dbo.Customer).</summary>
[Route("api/customers")]
[Authorize(Policy = Policies.Read)]
[AllowAnonymous] // TODO(auth go-live): remove this line to enforce the policies
public sealed class CustomersController(ICustomerService service)
    : CrudController<int, CustomerListQuery, CreateCustomerRequest, UpdateCustomerRequest, CustomerResponse>(service);
