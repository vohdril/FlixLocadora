using Microsoft.Extensions.Configuration;
using FlixLocadora.Database;

// Usage (from the repository root):
//   dotnet run --project tools/FlixLocadora.Database [-- --seed] [-- --connection-name AppDb]
// The connection string comes from the same places as the API's: appsettings(.Development).json,
// user-secrets (shared UserSecretsId) and the ConnectionStrings__AppDb environment variable. It is never printed.

var seed = args.Contains("--seed");
var connectionName = ArgumentValue("--connection-name") ?? "AppDb";

var root = FindRepositoryRoot();
var api = Path.Combine(root, "src", "FlixLocadora.Api");

var configuration = new ConfigurationBuilder()
    .AddJsonFile(Path.Combine(api, "appsettings.json"), optional: true)
    .AddJsonFile(Path.Combine(api, "appsettings.Development.json"), optional: true)
    .AddUserSecrets(typeof(DatabaseDeployer).Assembly, optional: true)
    .AddEnvironmentVariables()
    .Build();

var connectionString = configuration.GetConnectionString(connectionName);
if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.Error.WriteLine($"ConnectionStrings:{connectionName} is not configured (user-secrets or ConnectionStrings__{connectionName}).");
    return 2;
}

Console.WriteLine($"Applying database/schema{(seed ? " + database/seed" : "")} to ConnectionStrings:{connectionName}...");
return DatabaseDeployer.Deploy(connectionString, Path.Combine(root, "database"), seed, Console.Out) ? 0 : 1;

string? ArgumentValue(string name)
{
    var index = Array.IndexOf(args, name);
    return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
}

static string FindRepositoryRoot()
{
    for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
    {
        if (directory.GetFiles("*.slnx").Length > 0)
            return directory.FullName;
    }

    throw new InvalidOperationException("Repository root (*.slnx) not found above " + AppContext.BaseDirectory);
}
