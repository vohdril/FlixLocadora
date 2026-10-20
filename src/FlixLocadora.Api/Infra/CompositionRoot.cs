using System.Reflection;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection.Extensions;
using FlixLocadora.Api.Business.Common;
using FlixLocadora.Api.Data;

namespace FlixLocadora.Api.Infra;

/// <summary>
/// Builds the DI container by convention (Foundation.API CompositionRoot): adding a NEW entity does not touch
/// this file — services, repositories, converters and validators are discovered in the assembly.
/// One assembly only, so the hard-coded assembly list of the Decco version is not needed.
/// </summary>
public static class CompositionRoot
{
    public static IServiceCollection RegisterDependencies(this IServiceCollection services)
    {
        var assembly = typeof(CompositionRoot).Assembly;

        RegisterByMarker(services, assembly, typeof(IService));
        RegisterByMarker(services, assembly, typeof(IRepository));
        RegisterClosedGenerics(services, assembly, typeof(IEntityConverter<,,,>));

        // Registration only. EXECUTION is explicit, in Infra/Filters/RequestValidationFilter.
        // Internal validators (rules included by public ones) are not registered.
        services.AddValidatorsFromAssembly(assembly, ServiceLifetime.Scoped);

        services.TryAddSingleton(TimeProvider.System);

        return services;
    }

    /// <summary>
    /// Every concrete class implementing an interface derived from the marker is registered by that interface
    /// (e.g. CustomerService → ICustomerService). Open generic definitions are registered explicitly elsewhere.
    /// </summary>
    private static void RegisterByMarker(IServiceCollection services, Assembly assembly, Type marker)
    {
        var implementations = assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false } && marker.IsAssignableFrom(t));

        foreach (var implementation in implementations)
        {
            foreach (var contract in implementation.GetInterfaces().Where(i => i != marker && marker.IsAssignableFrom(i)))
                services.AddScoped(contract, implementation);
        }
    }

    /// <summary>Registers each concrete implementation of an open generic interface by its closed interface.</summary>
    private static void RegisterClosedGenerics(IServiceCollection services, Assembly assembly, Type openGeneric)
    {
        var implementations = assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false });

        foreach (var implementation in implementations)
        {
            foreach (var contract in implementation.GetInterfaces().Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == openGeneric))
                services.AddScoped(contract, implementation);
        }
    }
}
