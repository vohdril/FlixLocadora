using System.Text.Json;

namespace CrudGen;

/// <summary>
/// Optional crudgen.json at the repository root — per-entity decisions the database cannot express:
/// <code>
/// {
///   "entities": {
///     "Movie":  { "excludeFromCreate": [ "AvailableCopies" ], "excludeFromUpdate": [ "TotalCopies", "AvailableCopies" ] },
///     "Rental": { "skip": true }
///   }
/// }
/// </code>
/// Excluded columns leave the request contracts (the converter ignores them; the service sets them).
/// </summary>
public sealed class CrudGenConfig
{
    public Dictionary<string, EntityConfig> Entities { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    public static CrudGenConfig Load(string repositoryRoot)
    {
        var path = Path.Combine(repositoryRoot, "crudgen.json");
        if (!File.Exists(path))
            return new CrudGenConfig();

        var config = JsonSerializer.Deserialize<CrudGenConfig>(File.ReadAllText(path), new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        }) ?? new CrudGenConfig();

        return new CrudGenConfig { Entities = new Dictionary<string, EntityConfig>(config.Entities, StringComparer.OrdinalIgnoreCase) };
    }

    public EntityInfo Apply(EntityInfo entity)
    {
        if (!Entities.TryGetValue(entity.Name, out var settings))
            return entity;

        return entity with
        {
            Properties = entity.Properties.Select(p => p with
            {
                ExcludedFromCreate = settings.ExcludeFromCreate.Contains(p.Name, StringComparer.OrdinalIgnoreCase),
                ExcludedFromUpdate = settings.ExcludeFromUpdate.Contains(p.Name, StringComparer.OrdinalIgnoreCase)
            }).ToList()
        };
    }

    public bool Skips(string entity) => Entities.TryGetValue(entity, out var settings) && settings.Skip;
}

public sealed class EntityConfig
{
    public bool Skip { get; init; }

    public string[] ExcludeFromCreate { get; init; } = [];

    public string[] ExcludeFromUpdate { get; init; } = [];
}
