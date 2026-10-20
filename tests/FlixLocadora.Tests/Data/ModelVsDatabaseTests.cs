using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using FlixLocadora.Api.Data;
using FlixLocadora.Tests.Support;
using Xunit.Abstractions;

namespace FlixLocadora.Tests.Data;

/// <summary>
/// ARCHITECTURE TEST (Decco.API ModelVsDatabaseTests) — does the model EF builds match the database that exists?
/// In database-first, a mismatch fails SILENTLY until someone runs the query: it is how the "Anomalium" typo
/// survived in the Decco DataLayer. By default it checks the throwaway database built from database/schema;
/// set MODEL_DB_CONNECTION to check any other database (e.g. the case-study database of an interview).
/// </summary>
[Collection(ApiCollection.Name)]
[Trait("Category", "ModelVsDatabase")]
public class ModelVsDatabaseTests(ApiFixture fixture, ITestOutputHelper output)
{
    private string ConnectionString => Environment.GetEnvironmentVariable("MODEL_DB_CONNECTION") ?? fixture.ConnectionString;

    private AppDbContext CreateContext() => new(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(ConnectionString).Options);

    [Fact]
    public void Every_model_entity_points_to_an_existing_table_or_view()
    {
        using var context = CreateContext();
        using var connection = Open(context);
        var missing = new List<string>();

        foreach (var entity in context.Model.GetEntityTypes().OrderBy(e => e.ClrType.Name))
        {
            var name = QualifiedName(entity);
            var exists = name is not null && Scalar<int>(connection, "SELECT CASE WHEN OBJECT_ID(@name) IS NULL THEN 0 ELSE 1 END", name) == 1;
            output.WriteLine($"{(exists ? "ok  " : "FAIL")}  {entity.ClrType.Name,-28} → {name}");

            if (!exists)
                missing.Add($"{entity.ClrType.Name} → '{name ?? "(no table or view)"}' does not exist");
        }

        Assert.True(missing.Count == 0, string.Join(Environment.NewLine, missing));
    }

    [Fact]
    public void Every_mapped_property_matches_an_existing_column()
    {
        using var context = CreateContext();
        using var connection = Open(context);
        var missing = new List<string>();

        foreach (var entity in context.Model.GetEntityTypes())
        {
            if (QualifiedName(entity) is not { } name)
                continue;

            var columns = Strings(connection, "SELECT c.name FROM sys.columns c WHERE c.object_id = OBJECT_ID(@name)", name);
            if (columns.Count == 0)
                continue;   // a missing object is covered by the test above

            missing.AddRange(entity.GetProperties()
                .Select(p => (Property: p, Column: p.GetColumnName()))
                .Where(x => x.Column is not null && !columns.Contains(x.Column))
                .Select(x => $"{entity.ClrType.Name}.{x.Property.Name} → column '{name}.{x.Column}' does not exist"));
        }

        Assert.True(missing.Count == 0, string.Join(Environment.NewLine, missing));
    }

    [Fact]
    public void Every_table_of_the_domain_schemas_is_mapped()
    {
        using var context = CreateContext();
        using var connection = Open(context);

        var mapped = context.Model.GetEntityTypes().Select(QualifiedName).OfType<string>().ToHashSet(StringComparer.OrdinalIgnoreCase);
        var schemas = context.Model.GetEntityTypes().Select(e => e.GetSchema() ?? e.GetViewSchema() ?? "dbo").Append("dbo").Distinct().ToList();

        var unmapped = schemas
            .SelectMany(schema => Strings(connection, "SELECT QUOTENAME(s.name) + '.' + QUOTENAME(t.name) FROM sys.tables t JOIN sys.schemas s ON s.schema_id = t.schema_id WHERE s.name = @name", schema))
            .Select(table => table.Replace("[", "").Replace("]", ""))
            .Where(table => !mapped.Contains(table))
            .ToList();

        Assert.True(unmapped.Count == 0, "Tables without an entity — run scripts/scaffold.ps1: " + string.Join(", ", unmapped));
    }

    private static string? QualifiedName(Microsoft.EntityFrameworkCore.Metadata.IEntityType entity)
        => entity.GetTableName() is { } table ? $"{entity.GetSchema() ?? "dbo"}.{table}"
            : entity.GetViewName() is { } view ? $"{entity.GetViewSchema() ?? "dbo"}.{view}"
            : null;

    private static SqlConnection Open(AppDbContext context)
    {
        var connection = new SqlConnection(context.Database.GetConnectionString());
        connection.Open();
        return connection;
    }

    private static T Scalar<T>(SqlConnection connection, string sql, string name)
    {
        using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@name", name);
        return (T)command.ExecuteScalar()!;
    }

    private static HashSet<string> Strings(SqlConnection connection, string sql, string name)
    {
        using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@name", name);
        using var reader = command.ExecuteReader();
        var values = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (reader.Read())
            values.Add(reader.GetString(0));
        return values;
    }
}
