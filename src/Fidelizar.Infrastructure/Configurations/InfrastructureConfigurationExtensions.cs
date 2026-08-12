using Fidelizar.Domain.Repositories;
using Fidelizar.Infrastructure.Import;
using Fidelizar.Infrastructure.Persistence;
using Fidelizar.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Fidelizar.Infrastructure.Configurations;

/// <summary>
/// Composition for this layer: <see cref="FidelizarDbContext"/> and the per-aggregate
/// repositories. <c>Fidelizar.Api</c> calls this once at the composition root — it never talks
/// to a repository directly (ARCHITECTURE §3).
/// </summary>
public static class InfrastructureConfigurationExtensions
{
    public static IServiceCollection AddInfrastructureConfiguration(
        this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Falta 'ConnectionStrings:DefaultConnection'. No entra al repositorio (CLAUDE.md) " +
                "— configurarla con 'dotnet user-secrets' o la variable de entorno " +
                "ConnectionStrings__DefaultConnection.");
        }

        services.AddDbContext<FidelizarDbContext>(options => options.UseNpgsql(connectionString));

        services.AddScoped<IMovimientoRepository, MovimientoRepository>();
        services.AddScoped<ICorteRepository, CorteRepository>();
        services.AddScoped<IMiembroRepository, MiembroRepository>();

        // The padron importer (F0-08) is the entry door for every new business. It only depends
        // on Domain repository interfaces, registered just above.
        services.AddScoped<VipPadronImporter>();

        return services;
    }
}
