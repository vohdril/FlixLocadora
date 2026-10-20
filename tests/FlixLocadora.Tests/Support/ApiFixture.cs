using System.Net.Http.Headers;
using System.Security.Cryptography;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using FlixLocadora.Api.Business.Auth;
using FlixLocadora.Api.Data;
using FlixLocadora.Database;

namespace FlixLocadora.Tests.Support;

/// <summary>
/// One throwaway database per test run, created from database/schema with the same DbUp deployer the developers
/// use (so the scripts are tested too), and dropped at the end. The server comes from TEST_DB_CONNECTION
/// (any SQL Server — e.g. a container in CI) or defaults to LocalDB. Identity migrations, roles and an admin are
/// applied by the API's own startup seeder (Auth:SeedOnStartup), with credentials generated for this run.
/// </summary>
public sealed class ApiFixture : IAsyncLifetime
{
    public string ConnectionString { get; } = NewDatabase();

    public string AdminEmail { get; } = "admin@tests.local";

    public string AdminPassword { get; } = $"Adm!{Guid.NewGuid():N}aA1";

    public ApiFactory Factory { get; private set; } = null!;

    public Task InitializeAsync()
    {
        var log = new StringWriter();
        if (!DatabaseDeployer.Deploy(ConnectionString, RepositoryPaths.Database, seed: false, log))
            throw new InvalidOperationException("Could not create the test database:" + Environment.NewLine + log);

        Factory = new ApiFactory(ConnectionString, new Dictionary<string, string?>
        {
            ["Auth:SeedOnStartup"] = "true",
            ["Auth:AdminEmail"] = AdminEmail,
            ["Auth:AdminPassword"] = AdminPassword,
            ["Jwt:SigningKey"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)),
        });
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await Factory.DisposeAsync();
        SqlConnection.ClearAllPools();
        DatabaseDeployer.Drop(ConnectionString);
    }

    public HttpClient CreateClient(string? token = null)
    {
        var client = Factory.CreateClient();
        if (token is not null)
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    public async Task<string> LoginAsync(string email, string password)
    {
        using var client = CreateClient();
        var reply = await client.PostEnvelopeAsync<TokenResponse>("/api/auth/login", new LoginRequest(email, password));
        return reply.Data.AccessToken;
    }

    public Task<string> LoginAsAdminAsync() => LoginAsync(AdminEmail, AdminPassword);

    /// <summary>Arranges what the API refuses on purpose (e.g. a rental made days ago) straight in the test database.</summary>
    public async Task ArrangeAsync(Func<AppDbContext, Task> arrange)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        await arrange(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    /// <summary>Registers a new user (default role Operator) and returns its token.</summary>
    public async Task<(string Email, string Token)> NewOperatorAsync()
    {
        var email = $"user{Guid.NewGuid():N}@tests.local";
        const string password = "Senha!Forte123";
        using var client = CreateClient();
        await client.PostEnvelopeAsync<UserResponse>("/api/auth/register", new RegisterRequest(email, password));
        return (email, await LoginAsync(email, password));
    }

    private static string NewDatabase()
    {
        var server = Environment.GetEnvironmentVariable("TEST_DB_CONNECTION")
            ?? @"Server=(localdb)\MSSQLLocalDB;Trusted_Connection=True;TrustServerCertificate=True";

        return new SqlConnectionStringBuilder(server) { InitialCatalog = $"FlixLocadora_Test_{Guid.NewGuid():N}" }.ConnectionString;
    }
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFixture>
{
    public const string Name = "api";
}

public static class RepositoryPaths
{
    public static string Root { get; } = FindRoot();

    public static string Database => Path.Combine(Root, "database");

    public static string Api => Path.Combine(Root, "src", "FlixLocadora.Api");

    private static string FindRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (directory.GetFiles("*.slnx").Length > 0)
                return directory.FullName;
        }

        throw new InvalidOperationException("Repository root (*.slnx) not found.");
    }
}
