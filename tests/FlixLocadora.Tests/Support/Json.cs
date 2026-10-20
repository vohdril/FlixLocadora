using System.Text.Json;
using System.Text.Json.Serialization;

namespace FlixLocadora.Tests.Support;

/// <summary>The API's JSON shape: camelCase and enums as text.</summary>
public static class Json
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };
}
