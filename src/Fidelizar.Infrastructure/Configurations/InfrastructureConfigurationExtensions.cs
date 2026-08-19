using Fidelizar.Domain.Repositories;
using Fidelizar.Domain.Security;
using Fidelizar.Infrastructure.Import;
using Fidelizar.Infrastructure.Persistence;
using Fidelizar.Infrastructure.Repositories;
using Fidelizar.Infrastructure.Security;
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
        services.AddScoped<IUsuarioRepository, UsuarioRepository>();
        services.AddScoped<IRegistroAuditoriaRepository, RegistroAuditoriaRepository>();
        services.AddScoped<INegocioRepository, NegocioRepository>();
        services.AddScoped<IConsentimientoRepository, ConsentimientoRepository>();
        services.AddScoped<ISucursalRepository, SucursalRepository>();

        // ARCHITECTURE §3: Application depends on IPasswordHasher only, never on ASP.NET Core
        // Identity directly — this is the one place that wires the real implementation in.
        services.AddSingleton<IPasswordHasher, IdentityPasswordHasher>();

        // The padron importer (F0-08) is the entry door for every new business. It only depends
        // on Domain repository interfaces, registered just above.
        services.AddScoped<VipPadronImporter>();

        return services;
    }
}
