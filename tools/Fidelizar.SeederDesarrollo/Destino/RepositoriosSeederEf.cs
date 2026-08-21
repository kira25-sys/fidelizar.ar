using Fidelizar.Domain.Entities;
using Fidelizar.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Fidelizar.SeederDesarrollo.Destino;

public sealed class NegocioSeederRepositoryEf(FidelizarDbContext dbContext) : INegocioSeederRepository
{
    public Task<Negocio?> ObtenerPrimeroAsync(CancellationToken cancellationToken = default) =>
        dbContext.Negocios.OrderBy(n => n.Id).FirstOrDefaultAsync(cancellationToken);

    public async Task<Negocio> CrearAsync(Negocio negocio, CancellationToken cancellationToken = default)
    {
        dbContext.Negocios.Add(negocio);
        await dbContext.SaveChangesAsync(cancellationToken);
        return negocio;
    }
}

public sealed class EstadoBaseRepositoryEf(FidelizarDbContext dbContext) : IEstadoBaseRepository
{
    public async Task<bool> TieneEsquemaAsync(CancellationToken cancellationToken = default) =>
        (await dbContext.Database.GetAppliedMigrationsAsync(cancellationToken)).Any();

    public async Task<ConteoBase> ContarAsync(CancellationToken cancellationToken = default) =>
        new(
            await dbContext.Negocios.CountAsync(cancellationToken),
            await dbContext.Sucursales.CountAsync(cancellationToken),
            await dbContext.Usuarios.CountAsync(cancellationToken),
            await dbContext.Miembros.CountAsync(cancellationToken),
            await dbContext.MovimientosCredito.LongCountAsync(cancellationToken));
}
