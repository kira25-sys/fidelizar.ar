namespace Fidelizar.Api.Configurations;

/// <summary>
/// Gives the uptime monitor of ARCHITECTURE §14 a target. No dependency checks wired in yet
/// (there is no DbContext to ping until later in phase 0) — this starts as a liveness probe and
/// grows dependency checks (database, migrations applied) as Infrastructure gets a DbContext.
/// </summary>
public static class HealthCheckConfigurationExtensions
{
    public static IServiceCollection AddAppHealthChecks(this IServiceCollection services)
    {
        services.AddHealthChecks();
        return services;
    }
}
