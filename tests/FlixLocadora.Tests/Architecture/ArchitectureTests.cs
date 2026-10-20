using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using FlixLocadora.Api.Business.Common;
using FlixLocadora.Api.Data;
using FlixLocadora.Tests.Support;

namespace FlixLocadora.Tests.Architecture;

/// <summary>
/// Invariants that no single-class test catches. In the Decco solutions these boundaries are separate projects
/// (the compiler guards them); here the API is one project, so these tests guard them.
/// </summary>
[Collection(ApiCollection.Name)]
public class ArchitectureTests(ApiFixture fixture)
{
    private static readonly Assembly Api = typeof(Program).Assembly;
    private const string DataNamespace = "FlixLocadora.Api.Data";
    private const string EntitiesNamespace = "FlixLocadora.Api.Data.Entities";
    private const string BusinessNamespace = "FlixLocadora.Api.Business";

    [Fact]
    public void CompositionRoot_resolves_every_service_and_repository()
    {
        using var scope = fixture.Factory.Services.CreateScope();

        var contracts = Api.GetTypes().Where(t =>
            t is { IsInterface: true, IsGenericTypeDefinition: false }
            && t != typeof(IService) && t != typeof(IRepository)
            && (typeof(IService).IsAssignableFrom(t) || typeof(IRepository).IsAssignableFrom(t))
            // an extension point without implementations yet (e.g. IConstraintErrorCatalog) is not a gap
            && Api.GetTypes().Any(i => i is { IsClass: true, IsAbstract: false } && t.IsAssignableFrom(i)));

        var missing = contracts.Where(c => scope.ServiceProvider.GetService(c) is null).Select(c => c.Name).ToList();

        Assert.True(missing.Count == 0, "Not registered by convention: " + string.Join(", ", missing));
    }

    [Fact]
    public void Public_contracts_never_expose_entities()
    {
        var violations = Api.GetExportedTypes()
            .Where(t => t.Namespace?.StartsWith(BusinessNamespace, StringComparison.Ordinal) == true)
            .SelectMany(t => t.GetProperties().Select(p => (Type: t, Property: p)))
            .Where(x => ReferencedTypes(x.Property.PropertyType).Any(IsEntity))
            .Select(x => $"{x.Type.Name}.{x.Property.Name}")
            .ToList();

        Assert.True(violations.Count == 0, "Contract exposing an entity: " + string.Join(", ", violations));
    }

    [Fact]
    public void Controllers_do_not_depend_on_the_data_layer()
    {
        var violations = Api.GetTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract)
            .SelectMany(c => c.GetConstructors().SelectMany(ctor => ctor.GetParameters()).Select(p => (Controller: c, Parameter: p)))
            .Where(x => x.Parameter.ParameterType.Namespace?.StartsWith(DataNamespace, StringComparison.Ordinal) == true)
            .Select(x => $"{x.Controller.Name}({x.Parameter.ParameterType.Name})")
            .ToList();

        Assert.True(violations.Count == 0, "Controller using the DataLayer directly: " + string.Join(", ", violations));
    }

    [Fact]
    public void Business_does_not_depend_on_mvc()
    {
        const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        var violations = Api.GetTypes()
            .Where(t => t.Namespace?.StartsWith(BusinessNamespace, StringComparison.Ordinal) == true)
            .SelectMany(t => t.GetFields(all).Select(f => (Type: t, Used: f.FieldType))
                .Concat(t.GetProperties(all).Select(p => (Type: t, Used: p.PropertyType)))
                .Concat(t.GetMethods(all).SelectMany(m => m.GetParameters().Select(p => p.ParameterType).Append(m.ReturnType)).Select(u => (Type: t, Used: u)))
                .Concat(t.GetConstructors(all).SelectMany(c => c.GetParameters()).Select(p => (Type: t, Used: p.ParameterType))))
            .Where(x => ReferencedTypes(x.Used).Any(u => u.Namespace?.StartsWith("Microsoft.AspNetCore.Mvc", StringComparison.Ordinal) == true))
            .Select(x => $"{x.Type.Name} → {x.Used.Name}")
            .Distinct()
            .ToList();

        Assert.True(violations.Count == 0, "Business depending on MVC: " + string.Join(", ", violations));
    }

    /// <summary>
    /// THE SWITCH OF THE AUTHENTICATION (planning, 05 §4): Architecture/AnonymousEndpoints.txt lists the route
    /// prefixes allowed to be anonymous. At go-live, remove the testing-phase lines AND the [AllowAnonymous] lines
    /// from the controllers — the security posture changes in code review, visibly, never through a flag.
    /// CrudGen adds the routes of the slices it creates to the testing-phase section.
    /// </summary>
    private static readonly string[] AllowedAnonymous = File.ReadAllLines(Path.Combine(RepositoryPaths.Root, "tests", "FlixLocadora.Tests", "Architecture", "AnonymousEndpoints.txt"))
        .Select(line => line.Trim())
        .Where(line => line.Length > 0 && !line.StartsWith('#'))
        .ToArray();

    [Fact]
    public void Anonymous_endpoints_match_the_allowed_list()
    {
        var endpoints = fixture.Factory.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>();

        var unexpected = endpoints
            .Where(e => e.Metadata.GetMetadata<IAllowAnonymous>() is not null)
            .Select(e => e.RoutePattern.RawText?.TrimStart('/') ?? "")
            .Where(route => route.Length > 0 && !AllowedAnonymous.Any(allowed => route.StartsWith(allowed, StringComparison.OrdinalIgnoreCase)))
            .Distinct()
            .ToList();

        Assert.True(unexpected.Count == 0, "Anonymous endpoints outside the allowed list: " + string.Join(", ", unexpected));
    }

    private static bool IsEntity(Type type) => type.Namespace == EntitiesNamespace || typeof(IEntity).IsAssignableFrom(type);

    private static IEnumerable<Type> ReferencedTypes(Type type)
    {
        yield return type;

        if (type.HasElementType)
            foreach (var inner in ReferencedTypes(type.GetElementType()!))
                yield return inner;

        if (type.IsGenericType)
            foreach (var argument in type.GetGenericArguments().SelectMany(ReferencedTypes))
                yield return argument;
    }
}
