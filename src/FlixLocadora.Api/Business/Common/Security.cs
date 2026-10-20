namespace FlixLocadora.Api.Business.Common;

/// <summary>Identity roles. Seeded in Development (Infra/Auth/IdentitySeeder).</summary>
public static class Roles
{
    public const string Admin = "Admin";
    public const string Operator = "Operator";

    public static readonly IReadOnlyList<string> All = [Admin, Operator];
}

/// <summary>Authorization policies (Infra/Auth). Read = any authenticated user; Write = Admin or Operator; Admin = Admin.</summary>
public static class Policies
{
    public const string Read = "Read";
    public const string Write = "Write";
    public const string Admin = "Admin";
}
