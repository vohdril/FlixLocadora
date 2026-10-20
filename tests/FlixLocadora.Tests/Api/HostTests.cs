using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FlixLocadora.Api.Business.Common;
using FlixLocadora.Tests.Support;

namespace FlixLocadora.Tests.Api;

[Collection(ApiCollection.Name)]
public class HostTests(ApiFixture fixture)
{
    [Fact]
    public async Task Health_is_anonymous_and_echoes_the_request_id()
    {
        using var client = fixture.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Add("X-Request-Id", "test-123");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("test-123", response.Headers.GetValues("X-Request-Id").Single());
    }

    [Fact]
    public async Task OpenApi_document_is_anonymous_and_uses_openapi_3_1()
    {
        using var client = fixture.CreateClient();

        var document = await client.GetStringAsync("/openapi/v1.json");

        Assert.Contains("\"openapi\": \"3.1", document);
        Assert.Contains("\"ApiResponse\"", document);                       // StandardResponsesTransformer: 400/404/500 with the envelope
        Assert.Contains("Checks the password", document);                   // XML comments → OpenAPI (.NET 10)
        Assert.Contains("\"bearerFormat\": \"JWT\"", document);              // BearerSecuritySchemeTransformer
    }

    [Fact]
    public async Task Dates_are_strings_in_the_openapi_contract()
    {
        using var client = fixture.CreateClient();
        using var document = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json"));

        var rentedAt = document.RootElement.GetProperty("components").GetProperty("schemas")
            .GetProperty("RentalResponse").GetProperty("properties").GetProperty("rentedAt");

        Assert.Equal(("string", "date-time"), (rentedAt.GetProperty("type").GetString(), rentedAt.GetProperty("format").GetString()));
    }

    [Fact]
    public void Invalid_shop_rules_stop_the_api_at_startup()
    {
        using var factory = new ApiFactory(fixture.ConnectionString, new Dictionary<string, string?>
        {
            ["Auth:SeedOnStartup"] = "false",
            ["Rental:MaxDays"] = "0",
        });

        var error = Assert.ThrowsAny<Exception>(() => factory.CreateClient());

        Assert.Contains("RentalOptions.MaxDays", error.ToString());
    }

    [Fact]
    public async Task Unknown_route_is_a_404_envelope_not_a_401()
    {
        using var client = fixture.CreateClient();

        using var response = await client.GetAsync("/api/does-not-exist");
        var body = await response.Content.ReadFromJsonAsync<ApiResponse>(Json.Options);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(ResponseStatus.Fail, body!.Status);
        Assert.Equal("NOT_FOUND", body.Errors.Single().Code);
        Assert.False(string.IsNullOrWhiteSpace(body.RequestId));
    }
}
