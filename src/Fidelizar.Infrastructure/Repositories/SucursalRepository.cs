using Fidelizar.Domain.Entities;
using Fidelizar.Domain.Repositories;
using Fidelizar.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Fidelizar.Infrastructure.Repositories;

/// <summary>EF Core implementation of <see cref="ISucursalRepository"/>.</summary>
public sealed class SucursalRepository(FidelizarDbContext dbContext) : ISucursalRepository
{
    public Task<Sucursal?> GetByIdAsync(int negocioId, int id, CancellationToken cancellationToken = default) =>
        dbContext.Sucursales.SingleOrDefaultAsync(s => s.NegocioId == negocioId && s.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Sucursal>> ListarAsync(int negocioId, CancellationToken cancellationToken = default) =>
        await dbContext.Sucursales
            .Where(s => s.NegocioId == negocioId)
            .OrderBy(s => s.Nombre)
            .ToListAsync(cancellationToken);

    public async Task<Sucursal> AddAsync(Sucursal sucursal, CancellationToken cancellationToken = default)
    {
        dbContext.Sucursales.Add(sucursal);
        await dbContext.SaveChangesAsync(cancellationToken);
        return sucursal;
    }
}
