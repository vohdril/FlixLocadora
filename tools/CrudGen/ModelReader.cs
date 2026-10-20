using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Scaffolding;
using Microsoft.EntityFrameworkCore.SqlServer.Design.Internal;
using Microsoft.Extensions.DependencyInjection;

namespace CrudGen;

/// <summary>
/// Reads the database with EF Core's reverse-engineering services — the very model the T4 templates receive
/// during `dotnet ef dbcontext scaffold` — and reduces it to what the slices need.
/// </summary>
public static class ModelReader
{
    public static IModel Read(string connectionString, IReadOnlyList<string> schemas)
    {
        var services = new ServiceCollection().AddLogging();
        services.AddEntityFrameworkDesignTimeServices();
        new SqlServerDesignTimeServices().ConfigureDesignTimeServices(services);
        using var provider = services.BuildServiceProvider();

        var database = provider.GetRequiredService<IDatabaseModelFactory>()
            .Create(connectionString, new DatabaseModelFactoryOptions(tables: [], schemas: schemas));

        // Same options as scripts/scaffold.ps1 (no --use-database-names, pluralizer on).
        return provider.GetRequiredService<IScaffoldingModelFactory>()
            .Create(database, new ModelReverseEngineerOptions { UseDatabaseNames = false, NoPluralize = false });
    }

    public static IReadOnlyList<EntityInfo> Entities(IModel model)
        => model.GetEntityTypes()
            .Where(e => !e.IsSimpleManyToManyJoinEntityType())
            .OrderBy(e => e.Name, StringComparer.Ordinal)
            .Select(e => Describe(e, model))
            .ToList();

    private static EntityInfo Describe(IEntityType entity, IModel model)
    {
        var key = entity.FindPrimaryKey();
        var properties = entity.GetProperties()
            .OrderBy(p => p.GetColumnOrder() ?? -1)
            .Select(p => new PropInfo(
                Name: p.Name,
                Type: Types.Of(p.ClrType),
                IsNullable: p.IsNullable,
                IsString: p.ClrType == typeof(string),
                MaxLength: p.GetMaxLength(),
                Precision: p.GetPrecision(),
                Scale: p.GetScale(),
                IsKey: key?.Properties.Contains(p) == true,
                IsGeneratedKey: key?.Properties.Contains(p) == true && p.ValueGenerated != ValueGenerated.Never,
                IsRowVersion: p.IsConcurrencyToken && p.ValueGenerated == ValueGenerated.OnAddOrUpdate,
                IsComputed: p.GetComputedColumnSql() is not null,
                // A constant default ("DEFAULT (1)") may also carry its SQL text: it counts as a constant default only,
                // so the column stays updatable (IsActive, Status). Expressions (sysutcdatetime()) are server defaults.
                HasDefaultSql: p.GetDefaultValueSql() is not null && !HasConstantDefault(p),
                HasDefaultValue: HasConstantDefault(p),
                IsForeignKey: p.IsForeignKey(),
                IsAudit: p.Name is "UpdatedAt" or "ModifiedAt" or "UpdatedOn" && Nullable.GetUnderlyingType(p.ClrType) is { } t && (t == typeof(DateTime) || t == typeof(DateTimeOffset))))
            .ToList();

        var uniques = entity.GetIndexes()
            .Where(i => i.IsUnique && i.Properties.Count == 1 && !(key?.Properties.Contains(i.Properties[0]) ?? false))
            .Select(i => new UniqueInfo(i.Properties[0].Name, i.GetDatabaseName() ?? $"UQ_{entity.Name}_{i.Properties[0].Name}"))
            .OrderBy(u => u.Property, StringComparer.Ordinal)
            .ToList();

        var referencedBy = entity.GetReferencingForeignKeys()
            .Where(fk => fk.Properties.Count == 1 && fk.DeclaringEntityType != entity)
            .Select(fk => new ReferenceInfo(fk.DeclaringEntityType.Name, fk.Properties[0].Name, fk.GetConstraintName() ?? $"FK_{fk.DeclaringEntityType.Name}_{entity.Name}", fk.DeleteBehavior == DeleteBehavior.Cascade))
            .OrderBy(r => r.Entity, StringComparer.Ordinal)
            .ToList();

        var foreignKeys = entity.GetForeignKeys()
            .Where(fk => fk.Properties.Count == 1)
            .Select(fk => new ForeignKeyInfo(fk.Properties[0].Name, fk.PrincipalEntityType.Name, fk.GetConstraintName() ?? "", fk.IsRequired, fk.DeleteBehavior == DeleteBehavior.Cascade, fk.DependentToPrincipal?.Name))
            .ToList();

        static bool HasConstantDefault(IProperty property)
            => property.FindAnnotation(RelationalAnnotationNames.DefaultValue)?.Value is not null and not DBNull;

        return new EntityInfo(
            Name: entity.Name,
            Table: entity.GetTableName() is { } table ? $"{entity.GetSchema() ?? "dbo"}.{table}" : $"{entity.GetViewSchema() ?? "dbo"}.{entity.GetViewName()}",
            IsView: entity.GetTableName() is null,
            KeyProperties: key?.Properties.Select(p => p.Name).ToList() ?? [],
            Properties: properties,
            Navigations: entity.GetNavigations().Select(n => n.Name).Concat(entity.GetSkipNavigations().Select(n => n.Name)).ToList(),
            Uniques: uniques,
            ReferencedBy: referencedBy,
            ForeignKeys: foreignKeys);
    }
}

public sealed record PropInfo(
    string Name, string Type, bool IsNullable, bool IsString, int? MaxLength, int? Precision, int? Scale,
    bool IsKey, bool IsGeneratedKey, bool IsRowVersion, bool IsComputed, bool HasDefaultSql, bool HasDefaultValue,
    bool IsForeignKey, bool IsAudit, bool ExcludedFromCreate = false, bool ExcludedFromUpdate = false)
{
    /// <summary>Fields the client sends on POST: no generated key, rowversion, computed, server defaults or audit.</summary>
    public bool InCreate => !IsGeneratedKey && !IsRowVersion && !IsComputed && !HasDefaultSql && !HasDefaultValue && !IsAudit && !ExcludedFromCreate;

    /// <summary>Fields the client sends on PUT: constant-default columns (IsActive, Status) can be changed.</summary>
    public bool InUpdate => !IsKey && !IsRowVersion && !IsComputed && !HasDefaultSql && !IsAudit && !ExcludedFromUpdate;

    /// <summary>C# type with nullability ("string?", "int?", "decimal").</summary>
    public string CsType => IsNullable && !Type.EndsWith('?') ? Type + "?" : Type;
}

public sealed record UniqueInfo(string Property, string ConstraintName);

public sealed record ReferenceInfo(string Entity, string Property, string ConstraintName, bool Cascade);

public sealed record ForeignKeyInfo(string Property, string Principal, string ConstraintName, bool IsRequired, bool Cascade, string? Navigation);

public sealed record EntityInfo(
    string Name, string Table, bool IsView, IReadOnlyList<string> KeyProperties, IReadOnlyList<PropInfo> Properties,
    IReadOnlyList<string> Navigations, IReadOnlyList<UniqueInfo> Uniques, IReadOnlyList<ReferenceInfo> ReferencedBy,
    IReadOnlyList<ForeignKeyInfo> ForeignKeys)
{
    public PropInfo? Key => KeyProperties.Count == 1 ? Properties.Single(p => p.Name == KeyProperties[0]) : null;

    public PropInfo? RowVersion => Properties.FirstOrDefault(p => p.IsRowVersion);

    public PropInfo? Audit => Properties.FirstOrDefault(p => p.IsAudit);

    /// <summary>A required foreign key with ON DELETE CASCADE: the row belongs to its master (e.g. an order item).</summary>
    public ForeignKeyInfo? Master => ForeignKeys.FirstOrDefault(fk => fk.IsRequired && fk.Cascade);

    public IEnumerable<PropInfo> CreateFields => Properties.Where(p => p.InCreate);

    public IEnumerable<PropInfo> UpdateFields => Properties.Where(p => p.InUpdate);
}

public static class Types
{
    private static readonly Dictionary<Type, string> Keywords = new()
    {
        [typeof(string)] = "string", [typeof(bool)] = "bool", [typeof(byte)] = "byte", [typeof(short)] = "short",
        [typeof(int)] = "int", [typeof(long)] = "long", [typeof(decimal)] = "decimal", [typeof(double)] = "double",
        [typeof(float)] = "float", [typeof(Guid)] = "Guid", [typeof(DateTime)] = "DateTime", [typeof(DateTimeOffset)] = "DateTimeOffset",
        [typeof(DateOnly)] = "DateOnly", [typeof(TimeOnly)] = "TimeOnly", [typeof(TimeSpan)] = "TimeSpan", [typeof(byte[])] = "byte[]",
    };

    public static string Of(Type type)
    {
        var underlying = Nullable.GetUnderlyingType(type);
        if (underlying is not null)
            return Of(underlying) + "?";

        return Keywords.TryGetValue(type, out var keyword) ? keyword : type.FullName ?? type.Name;
    }
}
