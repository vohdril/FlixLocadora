using CrudGen;
using Microsoft.Extensions.Configuration;

// CrudGen — CRUD slices from the database model.
//
//   dotnet run --project tools/CrudGen -- [--entity Movie] [--prune] [--prune-owned] [--force] [--dry-run]
//                                          [--schemas dbo,sales] [--connection-name AppDb]
//
// For each table with a single-column key (views, composite keys and cascade details are skipped):
//   Business/<Feature>/Generated/*.g.cs   contracts, database rules, constraint errors  → rewritten on every run
//   Business/<Feature>/*.cs, Controllers/<Feature>Controller.cs, tests/.../<Feature>ApiTests.cs → written ONCE (yours)
// Plus docs/modelo-de-dados.md (Mermaid ER diagram) and the testing-phase list of anonymous endpoints.
// A feature folder without Generated/ is hand-written and is left alone (e.g. a feature you rewrote by hand).

var options = CliOptions.Parse(args);
var repository = Repository.Find();

var configuration = new ConfigurationBuilder()
    .AddJsonFile(Path.Combine(repository.Api, "appsettings.json"), optional: true)
    .AddJsonFile(Path.Combine(repository.Api, "appsettings.Development.json"), optional: true)
    .AddUserSecrets(typeof(CliOptions).Assembly, optional: true)
    .AddEnvironmentVariables()
    .Build();

var connectionString = configuration.GetConnectionString(options.ConnectionName);
if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.Error.WriteLine($"ConnectionStrings:{options.ConnectionName} is not configured (user-secrets or ConnectionStrings__{options.ConnectionName}).");
    return 2;
}

var config = CrudGenConfig.Load(repository.RootDirectory);
var entities = ModelReader.Entities(ModelReader.Read(connectionString, options.Schemas)).Select(config.Apply).ToList();
var writer = new SliceWriter(options.DryRun);
var managed = new List<string>();

foreach (var entity in entities)
{
    if (options.Entities.Count > 0 && !options.Entities.Contains(entity.Name, StringComparer.OrdinalIgnoreCase))
        continue;

    var slice = new Slice(entity, repository.Root);
    var featureFolder = Path.Combine(repository.Api, "Business", slice.Feature);
    var generatedFolder = Path.Combine(featureFolder, "Generated");

    var skip = entity switch
    {
        _ when config.Skips(entity.Name) => "skipped in crudgen.json (hand-written)",
        { IsView: true } => "view (read-only): write a query service by hand",
        { Key: null } => "no single-column primary key",
        { Master: { } master } => $"detail of {master.Principal} (ON DELETE CASCADE): handled by its master's slice",
        _ when Directory.Exists(featureFolder) && !Directory.Exists(generatedFolder) && !options.Force => "hand-written feature (no Generated/ folder)",
        _ => null
    };

    if (skip is not null)
    {
        writer.Report("skip", $"{entity.Name}: {skip}");
        continue;
    }

    managed.Add(slice.Name);

    writer.Generated(Path.Combine(generatedFolder, $"{slice.Name}Contracts.g.cs"), Generators.Contracts(slice));
    writer.Generated(Path.Combine(generatedFolder, $"{slice.Name}DatabaseRules.g.cs"), Generators.DatabaseRules(slice));
    writer.Generated(Path.Combine(generatedFolder, $"{slice.Name}Constraints.g.cs"), Generators.Constraints(slice));

    writer.Owned(Path.Combine(featureFolder, $"{slice.Name}Validators.cs"), Generators.Validators(slice), options.Force);
    writer.Owned(Path.Combine(featureFolder, $"{slice.Name}Converter.cs"), Generators.Converter(slice), options.Force);
    writer.Owned(Path.Combine(featureFolder, $"{slice.Name}Service.cs"), Generators.Service(slice), options.Force);
    writer.Owned(Path.Combine(repository.Api, "Controllers", $"{slice.Feature}Controller.cs"), Generators.Controller(slice), options.Force);
    writer.Owned(Path.Combine(repository.Tests, "Api", $"{slice.Feature}ApiTests.cs"), Generators.Tests(slice), options.Force);

    writer.AllowAnonymous(repository.AnonymousList, $"api/{slice.Route}");
}

if (options.Prune && options.Entities.Count == 0)
    writer.Prune(repository, entities.Select(e => e.Name).ToHashSet(StringComparer.Ordinal), options.PruneOwned);

writer.Generated(Path.Combine(repository.RootDirectory, "docs", "modelo-de-dados.md"), Generators.Diagram(entities));

writer.Summary();
return 0;

internal sealed record CliOptions(
    IReadOnlyList<string> Entities, IReadOnlyList<string> Schemas, string ConnectionName,
    bool Prune, bool PruneOwned, bool Force, bool DryRun)
{
    public static CliOptions Parse(string[] args)
    {
        var entities = new List<string>();
        var schemas = new List<string>();
        var connection = "AppDb";

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--entity" when i + 1 < args.Length: entities.AddRange(args[++i].Split(',', StringSplitOptions.RemoveEmptyEntries)); break;
                case "--schemas" when i + 1 < args.Length: schemas.AddRange(args[++i].Split(',', StringSplitOptions.RemoveEmptyEntries)); break;
                case "--connection-name" when i + 1 < args.Length: connection = args[++i]; break;
            }
        }

        return new CliOptions(entities, schemas.Count > 0 ? schemas : ["dbo"], connection,
            Prune: args.Contains("--prune") || args.Contains("--prune-owned"), PruneOwned: args.Contains("--prune-owned"),
            Force: args.Contains("--force"), DryRun: args.Contains("--dry-run"));
    }
}

internal sealed record Repository(string Root, string RootDirectory, string Api, string Tests, string AnonymousList)
{
    public static Repository Find()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var solution = directory.GetFiles("*.slnx").FirstOrDefault();
            if (solution is null)
                continue;

            var root = Path.GetFileNameWithoutExtension(solution.Name);
            var tests = Path.Combine(directory.FullName, "tests", $"{root}.Tests");
            return new Repository(root, directory.FullName, Path.Combine(directory.FullName, "src", $"{root}.Api"), tests,
                Path.Combine(tests, "Architecture", "AnonymousEndpoints.txt"));
        }

        throw new InvalidOperationException("Repository root (*.slnx) not found above " + AppContext.BaseDirectory);
    }
}

internal sealed class SliceWriter(bool dryRun)
{
    private readonly List<(string Action, string Detail)> _log = [];

    public void Report(string action, string detail) => _log.Add((action, detail));

    /// <summary>Generated file: rewritten when the content changed; removed when the generator has nothing to say.</summary>
    public void Generated(string path, string? content)
    {
        if (content is null)
        {
            if (File.Exists(path))
            {
                Delete(path);
                Report("removed", path);
            }
            return;
        }

        if (File.Exists(path) && Normalize(File.ReadAllText(path)) == Normalize(content))
            return;

        Report(File.Exists(path) ? "updated" : "created", path);
        Write(path, content);
    }

    /// <summary>Owned file: written only when missing (or with --force).</summary>
    public void Owned(string path, string content, bool force)
    {
        if (File.Exists(path) && !force)
            return;

        Report(File.Exists(path) ? "overwritten" : "created", path);
        Write(path, content);
    }

    /// <summary>Adds a route prefix to the testing-phase section of the anonymous-endpoints list.</summary>
    public void AllowAnonymous(string listPath, string prefix)
    {
        if (!File.Exists(listPath))
            return;

        var lines = File.ReadAllLines(listPath).ToList();
        if (lines.Any(l => l.Trim().Equals(prefix, StringComparison.OrdinalIgnoreCase)))
            return;

        var marker = lines.FindIndex(l => l.StartsWith("# testing phase", StringComparison.OrdinalIgnoreCase));
        lines.Insert(marker >= 0 ? marker + 1 : lines.Count, prefix);
        Report("anonymous", $"{prefix} (testing phase — remove at the auth go-live)");
        if (!dryRun)
            File.WriteAllLines(listPath, lines);
    }

    /// <summary>Removes the generated files of entities that no longer exist; lists (or removes) their owned files.</summary>
    public void Prune(Repository repository, IReadOnlySet<string> entities, bool removeOwned)
    {
        var business = Path.Combine(repository.Api, "Business");
        if (!Directory.Exists(business))
            return;

        foreach (var generated in Directory.EnumerateDirectories(business, "Generated", SearchOption.AllDirectories).ToList())
        {
            foreach (var file in Directory.EnumerateFiles(generated, "*Contracts.g.cs"))
            {
                var entity = Path.GetFileName(file)[..^"Contracts.g.cs".Length];
                if (entities.Contains(entity))
                    continue;

                var feature = Path.GetFileName(Path.GetDirectoryName(generated)!);
                foreach (var stale in Directory.EnumerateFiles(generated, $"{entity}*.g.cs"))
                {
                    Delete(stale);
                    Report("removed", stale);
                }

                var owned = new[]
                {
                    Path.Combine(business, feature, $"{entity}Validators.cs"),
                    Path.Combine(business, feature, $"{entity}Converter.cs"),
                    Path.Combine(business, feature, $"{entity}Service.cs"),
                    Path.Combine(repository.Api, "Controllers", $"{feature}Controller.cs"),
                    Path.Combine(repository.Tests, "Api", $"{feature}ApiTests.cs"),
                }.Where(File.Exists);

                foreach (var file2 in owned)
                {
                    if (removeOwned)
                    {
                        Delete(file2);
                        Report("removed", file2);
                    }
                    else
                    {
                        Report("orphan", $"{file2} (entity {entity} is gone — review and delete; or rerun with --prune-owned)");
                    }
                }

                if (removeOwned && !dryRun && Directory.Exists(generated) && !Directory.EnumerateFileSystemEntries(generated).Any())
                    Directory.Delete(generated);
                if (removeOwned && !dryRun && Directory.Exists(Path.Combine(business, feature)) && !Directory.EnumerateFileSystemEntries(Path.Combine(business, feature)).Any())
                    Directory.Delete(Path.Combine(business, feature));
            }
        }
    }

    public void Summary()
    {
        foreach (var (action, detail) in _log)
            Console.WriteLine($"{action,-11} {detail}");

        var changes = _log.Count(entry => entry.Action is not "skip");
        Console.WriteLine(changes == 0 ? "Nothing to do: the slices are in sync with the database." : $"{changes} change(s){(dryRun ? " (dry run — nothing written)" : "")}.");
    }

    private void Write(string path, string content)
    {
        if (dryRun)
            return;

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content.Replace("\r\n", "\n"));
    }

    private void Delete(string path)
    {
        if (!dryRun)
            File.Delete(path);
    }

    private static string Normalize(string text) => text.Replace("\r\n", "\n").TrimEnd();
}
