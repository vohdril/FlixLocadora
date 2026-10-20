using Microsoft.Extensions.Options;
using FlixLocadora.Api.Business.Rentals;
using FlixLocadora.Api.Infra.Auth;

namespace FlixLocadora.Api.Infra;

public static class DomainOptions
{
    /// <summary>Business settings of the shop, validated at startup (a wrong value stops the API, not a rental).</summary>
    public static IServiceCollection AddDomainOptions(this IServiceCollection services)
    {
        services.AddOptions<RentalOptions>().BindConfiguration(RentalOptions.Section).ValidateOnStart();
        services.AddSingleton<IValidateOptions<RentalOptions>, FluentValidateOptions<RentalOptions>>();
        return services;
    }
}
