using Fidelizar.Domain.Entities;
using Fidelizar.Domain.Repositories;
using Fidelizar.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Fidelizar.Infrastructure.Repositories;

/// <summary>EF Core implementation of <see cref="IMiembroRepository"/>.</summary>
public sealed class MiembroRepository(FidelizarDbContext dbContext) : IMiembroRepository
{
    public Task<Miembro?> GetByClienteExternoIdAsync(
        int negocioId, string clienteExternoId, CancellationToken cancellationToken = default) =>
        dbContext.Miembros.SingleOrDefaultAsync(
            m => m.NegocioId == negocioId && m.ClienteExternoId == clienteExternoId, cancellationToken);

    public async Task<Miembro> AddAsync(Miembro miembro, CancellationToken cancellationToken = default)
    {
        dbContext.Miembros.Add(miembro);
        await dbContext.SaveChangesAsync(cancellationToken);
        return miembro;
    }
}
