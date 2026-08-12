using Fidelizar.Domain.Entities;
using Fidelizar.Domain.Repositories;
using Fidelizar.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Fidelizar.Infrastructure.Repositories;

/// <summary>EF Core implementation of <see cref="ICorteRepository"/>.</summary>
public sealed class CorteRepository(FidelizarDbContext dbContext) : ICorteRepository
{
    public Task<Corte?> ObtenerAsync(int negocioId, CancellationToken cancellationToken = default) =>
        dbContext.Cortes.SingleOrDefaultAsync(c => c.NegocioId == negocioId, cancellationToken);

    public async Task<Corte> DeclararAsync(Corte corte, CancellationToken cancellationToken = default)
    {
        dbContext.Cortes.Add(corte);
        await dbContext.SaveChangesAsync(cancellationToken);
        return corte;
    }
}
