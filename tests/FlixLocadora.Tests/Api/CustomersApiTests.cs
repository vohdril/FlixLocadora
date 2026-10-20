// Created by CrudGen — this file is yours: CrudGen never overwrites it.
using System.Net;
using FlixLocadora.Api.Business.Common;
using FlixLocadora.Api.Business.Customers;
using FlixLocadora.Tests.Support;

namespace FlixLocadora.Tests.Api;

/// <summary>Smoke tests created by CrudGen — the real scenarios of Customers go here.</summary>
[Collection(ApiCollection.Name)]
public class CustomersApiTests(ApiFixture fixture)
{
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
