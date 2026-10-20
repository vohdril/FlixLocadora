using System.Net;
using System.Net.Http.Json;
using FlixLocadora.Api.Business.Common;

namespace FlixLocadora.Tests.Support;

/// <summary>A response read as the envelope: HTTP status + ApiResponse.</summary>
public sealed record Reply<T>(HttpStatusCode Status, ApiResponse<T> Body)
{
    public T Data => Body.Data ?? throw new InvalidOperationException($"No data (status {(int)Status}): {Describe()}");

    public Error Error => Body.Errors.Count == 1 ? Body.Errors[0] : throw new InvalidOperationException($"Expected one error: {Describe()}");

    public string Describe() => $"{Body.Status} [{string.Join(", ", Body.Errors.Select(e => $"{e.Code}@{e.Field}"))}]";

    public static async Task<Reply<T>> From(HttpResponseMessage response)
    {
        using (response)
        {
            var body = await response.Content.ReadFromJsonAsync<ApiResponse<T>>(Json.Options);
            return new Reply<T>(response.StatusCode, body ?? throw new InvalidOperationException("Empty body."));
        }
    }
}

public static class HttpExtensions
{
    public static async Task<Reply<T>> GetEnvelopeAsync<T>(this HttpClient client, string url)
        => await Reply<T>.From(await client.GetAsync(url));

    public static async Task<Reply<T>> PostEnvelopeAsync<T>(this HttpClient client, string url, object? body = null)
        => await Reply<T>.From(await client.PostAsJsonAsync(url, body, Json.Options));

    public static async Task<Reply<T>> PutEnvelopeAsync<T>(this HttpClient client, string url, object body)
        => await Reply<T>.From(await client.PutAsJsonAsync(url, body, Json.Options));

    public static async Task<Reply<T>> PatchEnvelopeAsync<T>(this HttpClient client, string url, object body)
        => await Reply<T>.From(await client.PatchAsJsonAsync(url, body, Json.Options));

    public static async Task<Reply<object>> DeleteEnvelopeAsync(this HttpClient client, string url)
        => await Reply<object>.From(await client.DeleteAsync(url));
}
