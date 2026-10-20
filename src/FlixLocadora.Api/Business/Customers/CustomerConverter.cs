// Created by CrudGen — this file is yours: CrudGen never overwrites it.
using Riok.Mapperly.Abstractions;
using FlixLocadora.Api.Business.Common;
using FlixLocadora.Api.Data.Entities;

namespace FlixLocadora.Api.Business.Customers;

/// <summary>Customer ⇄ contracts (Mapperly: a member without a counterpart is a build error).</summary>
[Mapper(AutoUserMappings = false)]
public sealed partial class CustomerConverter : IEntityConverter<Customer, CreateCustomerRequest, UpdateCustomerRequest, CustomerResponse>
{
    [MapperIgnoreTarget(nameof(Customer.Id))]
    [MapperIgnoreTarget(nameof(Customer.IsActive))]
    [MapperIgnoreTarget(nameof(Customer.CreatedAt))]
    [MapperIgnoreTarget(nameof(Customer.Rentals))]
    public partial Customer ToEntity(CreateCustomerRequest request);

    [MapperIgnoreTarget(nameof(Customer.Id))]
    [MapperIgnoreTarget(nameof(Customer.CreatedAt))]
    [MapperIgnoreTarget(nameof(Customer.Rentals))]
    public partial void Apply(UpdateCustomerRequest request, [MappingTarget] Customer entity);

    [MapperIgnoreSource(nameof(Customer.Rentals))]
    public partial CustomerResponse ToResponse(Customer entity);
}
