using Microsoft.EntityFrameworkCore;
using FlixLocadora.Api.Data.Repositories;

namespace FlixLocadora.Api.Data;

public static class DataLayer
{
    public const string ConnectionName = "AppDb";

    /// <summary>
    /// One connection string, one name: ConnectionStrings:AppDb (appsettings.Development.json → user-secrets →
    /// ConnectionStrings__AppDb). The same name feeds `dotnet ef` (Name=ConnectionStrings:AppDb) and the DbUp tool.
    /// Read when the context is built (not at registration), so test hosts can override it.
    /// </summary>
    public static IServiceCollection AddDataLayer(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>((provider, options) =>
        {
            var connectionString = provider.GetRequiredService<IConfiguration>().GetConnectionString(ConnectionName);
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new InvalidOperationException($"ConnectionStrings:{ConnectionName} is not configured (appsettings.Development.json, user-secrets or the ConnectionStrings__{ConnectionName} environment variable).");

            options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure());
        });

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));

        services.AddHealthChecks().AddDbContextCheck<AppDbContext>("database");

        return services;
    }
}
