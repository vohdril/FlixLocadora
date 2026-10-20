// Created by CrudGen — this file is yours: CrudGen never overwrites it.
using Microsoft.EntityFrameworkCore;
using FlixLocadora.Api.Business.Common;
using FlixLocadora.Api.Data;
using FlixLocadora.Api.Data.Entities;
using FlixLocadora.Api.Data.Repositories;

namespace FlixLocadora.Api.Business.Customers;

public interface ICustomerService : ICrudService<int, CustomerListQuery, CreateCustomerRequest, UpdateCustomerRequest, CustomerResponse>;

/// <summary>
/// Customers — the CRUD comes from CrudService; business rules go in the hooks below
/// (Filter, Order, OnCreating/OnUpdating, Check*). Unique columns are pre-checked for a friendly 409;
/// the database constraints stay the real guarantee (CustomerConstraints).
/// </summary>
public sealed class CustomerService(
    IRepository<Customer> customers,
    IRepository<Rental> rentals,
    IUnitOfWork unitOfWork,
    IEntityConverter<Customer, CreateCustomerRequest, UpdateCustomerRequest, CustomerResponse> converter)
    : CrudService<Customer, int, CustomerListQuery, CreateCustomerRequest, UpdateCustomerRequest, CustomerResponse>(customers, unitOfWork, converter),
      ICustomerService
{
    protected override IQueryable<Customer> Filter(IQueryable<Customer> query, CustomerListQuery parameters)
    {
        if (string.IsNullOrWhiteSpace(parameters.Search))
            return query;

        var search = parameters.Search.Trim();
        return query.Where(e => e.Name.Contains(search) || e.Email.Contains(search));
    }

    protected override IOrderedQueryable<Customer> Order(IQueryable<Customer> query) => query.OrderBy(e => e.Name).ThenBy(e => e.Id);

    protected override async Task<ApiResponse?> CheckCreateAsync(CreateCustomerRequest request, Customer entity, CancellationToken ct)
    {
        if (await Repository.Query().AnyAsync(e => e.Cpf == entity.Cpf, ct))
            return Responses.Fail(CustomerErrors.CpfAlreadyExists, field: "cpf");

        if (await Repository.Query().AnyAsync(e => e.Email == entity.Email, ct))
            return Responses.Fail(CustomerErrors.EmailAlreadyExists, field: "email");

        return null;
    }

    protected override async Task<ApiResponse?> CheckUpdateAsync(UpdateCustomerRequest request, Customer entity, CancellationToken ct)
    {
        if (await Repository.Query().AnyAsync(e => e.Cpf == entity.Cpf && e.Id != entity.Id, ct))
            return Responses.Fail(CustomerErrors.CpfAlreadyExists, field: "cpf");

        if (await Repository.Query().AnyAsync(e => e.Email == entity.Email && e.Id != entity.Id, ct))
            return Responses.Fail(CustomerErrors.EmailAlreadyExists, field: "email");

        return null;
    }

    protected override async Task<ApiResponse?> CheckDeleteAsync(Customer entity, CancellationToken ct)
    {
        if (await rentals.Query().AnyAsync(r => r.CustomerId == entity.Id, ct))
            return Responses.Fail(CustomerErrors.HasRentals);

        return null;
    }
}
