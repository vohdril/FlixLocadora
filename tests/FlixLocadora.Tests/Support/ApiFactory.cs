using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace FlixLocadora.Tests.Support;

/// <summary>
/// The API in memory, pointed at the throwaway test database. Development environment = ValidateScopes +
/// ValidateOnBuild: building the host already proves that the whole DI graph resolves.
/// </summary>
public class ApiFactory(string connectionString, IDictionary<string, string?>? settings = null) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration(configuration =>
        {
            var values = new Dictionary<string, string?> { ["ConnectionStrings:AppDb"] = connectionString };
            foreach (var (key, value) in settings ?? new Dictionary<string, string?>())
                values[key] = value;

            configuration.AddInMemoryCollection(values);
        });
    }
}
