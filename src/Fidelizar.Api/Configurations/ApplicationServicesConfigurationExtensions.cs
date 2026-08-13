using Fidelizar.Application.Services;

namespace Fidelizar.Api.Configurations;

/// <summary>
/// Registers <c>Fidelizar.Application</c>'s use cases. Lives here rather than in Application
/// itself so that project stays free of any dependency-injection package reference — Api is the
/// composition root (ARCHITECTURE §3).
/// </summary>
public static class ApplicationServicesConfigurationExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<ISaldoService, SaldoService>();
        services.AddScoped<ICorteService, CorteService>();
        services.AddScoped<IAuthService, AuthService>();

        return services;
    }
}
