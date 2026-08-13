using Fidelizar.Domain.Entities;
using Fidelizar.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Fidelizar.MigracionOctaviano.Destino;

public sealed class NegocioRepositoryEf(FidelizarDbContext dbContext) : INegocioRepository
{
    public Task<Negocio?> ObtenerUnicoAsync(CancellationToken cancellationToken = default) =>
        dbContext.Negocios.OrderBy(n => n.Id).FirstOrDefaultAsync(cancellationToken);

    public async Task<Negocio> CrearAsync(Negocio negocio, CancellationToken cancellationToken = default)
    {
        dbContext.Negocios.Add(negocio);
        await dbContext.SaveChangesAsync(cancellationToken);
        return negocio;
    }
}

public sealed class ConfiguracionProgramaRepositoryEf(FidelizarDbContext dbContext) : IConfiguracionProgramaRepository
{
    public Task<ConfiguracionPrograma?> ObtenerVigenteAsync(int negocioId, CancellationToken cancellationToken = default) =>
        dbContext.ConfiguracionesPrograma
            .SingleOrDefaultAsync(c => c.NegocioId == negocioId && c.VigenteHasta == null, cancellationToken);

    public async Task<ConfiguracionPrograma> CrearAsync(
        ConfiguracionPrograma configuracion, CancellationToken cancellationToken = default)
    {
        dbContext.ConfiguracionesPrograma.Add(configuracion);
        await dbContext.SaveChangesAsync(cancellationToken);
        return configuracion;
    }
}

public sealed class ConsentimientoRepositoryEf(FidelizarDbContext dbContext) : IConsentimientoRepository
{
    public Task<bool> ExisteAsync(
        int negocioId, int miembroId, TipoConsentimiento tipo, CancellationToken cancellationToken = default) =>
        dbContext.Consentimientos.AnyAsync(
            c => c.NegocioId == negocioId && c.MiembroId == miembroId && c.Tipo == tipo, cancellationToken);

    public async Task<Consentimiento> CrearAsync(
        Consentimiento consentimiento, CancellationToken cancellationToken = default)
    {
        dbContext.Consentimientos.Add(consentimiento);
        await dbContext.SaveChangesAsync(cancellationToken);
        return consentimiento;
    }
}
