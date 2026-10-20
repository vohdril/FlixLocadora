// Created by CrudGen — this file is yours: CrudGen never overwrites it.
using System.Net;
using FlixLocadora.Api.Business.Common;
using FlixLocadora.Api.Business.Customers;
using FlixLocadora.Tests.Support;

namespace FlixLocadora.Tests.Api;

/// <summary>Customers: the CrudGen smoke tests and the document rules.</summary>
[Collection(ApiCollection.Name)]
public class CustomersApiTests(ApiFixture fixture)
{
    private readonly HttpClient _client = fixture.CreateClient();

    [Fact]
    public async Task An_invalid_cpf_answers_400_with_its_own_code()
    {
        var reply = await _client.PostEnvelopeAsync<CustomerResponse>("/api/customers", TestData.NewCustomer() with { Cpf = "12345678900" });

        Assert.Equal(HttpStatusCode.BadRequest, reply.Status);
        Assert.Equal((CustomerErrors.CpfInvalid.Code, "cpf"), (reply.Error.Code, reply.Error.Field));
    }

    [Fact]
    public async Task A_repeated_cpf_answers_409()
    {
        var first = await _client.NewCustomerAsync();

        var reply = await _client.PostEnvelopeAsync<CustomerResponse>("/api/customers", TestData.NewCustomer() with { Cpf = first.Cpf });

        Assert.Equal((HttpStatusCode.Conflict, CustomerErrors.CpfAlreadyExists.Code, "cpf"), (reply.Status, reply.Error.Code, reply.Error.Field));
    }

    [Fact]
    public async Task Email_is_stored_lower_case_and_phone_as_digits()
    {
        var request = TestData.NewCustomer() with { Phone = "(11) 98765-4321" };

        var customer = (await _client.PostEnvelopeAsync<CustomerResponse>("/api/customers", request with { Email = request.Email.ToUpperInvariant() })).Data;

        Assert.Equal((request.Email.ToLowerInvariant(), "11987654321"), (customer.Email, customer.Phone));
    }

    [Fact]
    public async Task A_customer_with_rentals_cannot_be_deleted()
    {
        var customer = await _client.NewCustomerAsync();
        await _client.RentAsync((await _client.NewMovieAsync()).Id, customer.Id);

        var reply = await _client.DeleteEnvelopeAsync($"/api/customers/{customer.Id}");

        Assert.Equal((HttpStatusCode.Conflict, CustomerErrors.HasRentals.Code), (reply.Status, reply.Error.Code));
    }

    [Fact]
    public async Task List_answers_200_with_a_page()
    {
        using var client = fixture.CreateClient();

        var reply = await client.GetEnvelopeAsync<PagedResult<CustomerResponse>>("/api/customers?pageSize=5");

        Assert.Equal(HttpStatusCode.OK, reply.Status);
    }

    [Fact]
    public async Task Get_of_an_unknown_id_answers_404()
    {
        using var client = fixture.CreateClient();

        var reply = await client.GetEnvelopeAsync<CustomerResponse>($"/api/customers/987654321");

        Assert.Equal(HttpStatusCode.NotFound, reply.Status);
    }
}
