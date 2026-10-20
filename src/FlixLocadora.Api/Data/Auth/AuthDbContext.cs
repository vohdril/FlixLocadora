using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace FlixLocadora.Api.Data.Auth;

/// <summary>The application user (ASP.NET Core Identity: password hash, lockout, roles).</summary>
public sealed class AppUser : IdentityUser;

/// <summary>
/// Identity's own context, in the "auth" schema and versioned by MIGRATIONS (code-first): Identity is Microsoft's
/// code, and migrations are its supported path. The domain stays database-first in AppDbContext — the scaffold
/// reads only "dbo", so these tables never become entities (in the base projects the AspNet* classes were
/// scaffolded into Models/ and the generated context was hand-edited to inherit IdentityDbContext).
/// </summary>
public sealed class AuthDbContext(DbContextOptions<AuthDbContext> options) : IdentityDbContext<AppUser>(options)
{
    public const string Schema = "auth";

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.HasDefaultSchema(Schema);
    }
}
