// Created by CrudGen — this file is yours: CrudGen never overwrites it.
using Riok.Mapperly.Abstractions;
using FlixLocadora.Api.Business.Common;
using FlixLocadora.Api.Data.Entities;

namespace FlixLocadora.Api.Business.Customers;

/// <summary>Customer ⇄ contracts. CPF and phone are stored as digits; e-mail trimmed and lower-cased.</summary>
[Mapper(AutoUserMappings = false)]
public sealed partial class CustomerConverter : IEntityConverter<Customer, CreateCustomerRequest, UpdateCustomerRequest, CustomerResponse>
{
    [MapperIgnoreTarget(nameof(Customer.Id))]
    [MapperIgnoreTarget(nameof(Customer.IsActive))]
    [MapperIgnoreTarget(nameof(Customer.CreatedAt))]
    [MapperIgnoreTarget(nameof(Customer.Rentals))]
    [MapProperty(nameof(CreateCustomerRequest.Cpf), nameof(Customer.Cpf), Use = nameof(NormalizeCpf))]
    [MapProperty(nameof(CreateCustomerRequest.Email), nameof(Customer.Email), Use = nameof(NormalizeEmail))]
    [MapProperty(nameof(CreateCustomerRequest.Phone), nameof(Customer.Phone), Use = nameof(NormalizePhone))]
    public partial Customer ToEntity(CreateCustomerRequest request);

    [MapperIgnoreTarget(nameof(Customer.Id))]
    [MapperIgnoreTarget(nameof(Customer.CreatedAt))]
    [MapperIgnoreTarget(nameof(Customer.Rentals))]
    [MapProperty(nameof(UpdateCustomerRequest.Cpf), nameof(Customer.Cpf), Use = nameof(NormalizeCpf))]
    [MapProperty(nameof(UpdateCustomerRequest.Email), nameof(Customer.Email), Use = nameof(NormalizeEmail))]
    [MapProperty(nameof(UpdateCustomerRequest.Phone), nameof(Customer.Phone), Use = nameof(NormalizePhone))]
    public partial void Apply(UpdateCustomerRequest request, [MappingTarget] Customer entity);

    [MapperIgnoreSource(nameof(Customer.Rentals))]
    public partial CustomerResponse ToResponse(Customer entity);

    [UserMapping(Default = false)]
    private static string NormalizeCpf(string cpf) => Cpf.Normalize(cpf);

    [UserMapping(Default = false)]
    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    [UserMapping(Default = false)]
    private static string? NormalizePhone(string? phone) => Text.DigitsOrNull(phone);
}
