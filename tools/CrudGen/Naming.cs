using System.Text.RegularExpressions;

namespace CrudGen;

/// <summary>Names derived from an entity: Movie → Movies (feature), movies (route), MOVIE (error prefix).</summary>
public static partial class Naming
{
    public static string Plural(string name)
    {
        if (name.EndsWith('y') && name.Length > 1 && !"aeiou".Contains(char.ToLowerInvariant(name[^2])))
            return name[..^1] + "ies";

        return name.EndsWith('s') || name.EndsWith('x') || name.EndsWith('z') || name.EndsWith("ch") || name.EndsWith("sh")
            ? name + "es"
            : name + "s";
    }

    /// <summary>PurchaseOrders → purchase-orders.</summary>
    public static string Kebab(string name) => Words().Replace(name, "-$1").ToLowerInvariant();

    /// <summary>PurchaseOrder → PURCHASE_ORDER.</summary>
    public static string UpperSnake(string name) => Words().Replace(name, "_$1").ToUpperInvariant();

    /// <summary>PurchaseOrder → purchase order.</summary>
    public static string Spaced(string name) => Words().Replace(name, " $1").ToLowerInvariant();

    /// <summary>Name → name (JSON field of the envelope).</summary>
    public static string Camel(string name) => name.Length == 0 ? name : char.ToLowerInvariant(name[0]) + name[1..];

    [GeneratedRegex("(?<!^)([A-Z])")]
    private static partial Regex Words();
}
