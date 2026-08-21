using Fidelizar.Api.Monitoreo;
using Fidelizar.Api.Options;

namespace Fidelizar.Api.Configurations;

/// <summary>
/// Gives the external uptime monitor of ARCHITECTURE §14 its targets: <c>/health</c> and
/// <c>/health/live</c> answer while the process is up, <c>/health/ready</c> only while the
/// database also responds. See docs/OPERACION-MONITOREO.md.
/// </summary>
public static class HealthCheckConfigurationExtensions
{
    public static IServiceCollection AddAppHealthChecks(
        this IServiceCollection services, IConfiguration configuration)
    {
        // Bound once here (ARCHITECTURE §15). AddSerilogConfiguration binds the same section by
        // hand because it runs before there is a container to resolve IOptions from.
        services.Configure<MonitoreoSettings>(
            configuration.GetSection(MonitoreoSettings.SeccionConfiguracion));

        services
            .AddHealthChecks()
            .AddCheck<ChequeoBaseDeDatos>(
                OpcionesDeSalud.ChequeoBase, tags: [OpcionesDeSalud.TagReady]);

        return services;
    }
}
