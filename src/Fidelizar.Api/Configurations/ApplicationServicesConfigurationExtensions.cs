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
        services.AddScoped<IConsentimientoService, ConsentimientoService>();
        services.AddScoped<IMiembroBusquedaService, MiembroBusquedaService>();
        services.AddScoped<IFichaMostradorService, FichaMostradorService>();
        services.AddScoped<IFichaCompletaService, FichaCompletaService>();
        services.AddScoped<IHistorialMovimientosService, HistorialMovimientosService>();
        services.AddScoped<IAnulacionMovimientoService, AnulacionMovimientoService>();
        services.AddScoped<ICierreDiarioService, CierreDiarioService>();
        services.AddScoped<IUsuarioService, UsuarioService>();
        services.AddScoped<ISucursalService, SucursalService>();

        return services;
    }
}
