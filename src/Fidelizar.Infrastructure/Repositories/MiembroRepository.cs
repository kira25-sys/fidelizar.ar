using Fidelizar.Domain.Entities;
using Fidelizar.Domain.Repositories;
using Fidelizar.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Fidelizar.Infrastructure.Repositories;

/// <summary>EF Core implementation of <see cref="IMiembroRepository"/>.</summary>
public sealed class MiembroRepository(FidelizarDbContext dbContext) : IMiembroRepository
{
    /// <summary>S2 never becomes S-list: capped so a search stays a handful of homonym
    /// candidates, never a scroll through the roster (FUNCTIONAL-SPEC §4).</summary>
    private const int MaxResultadosBusqueda = 25;

    public Task<Miembro?> GetByClienteExternoIdAsync(
        int negocioId, string clienteExternoId, CancellationToken cancellationToken = default) =>
        dbContext.Miembros.SingleOrDefaultAsync(
            m => m.NegocioId == negocioId && m.ClienteExternoId == clienteExternoId, cancellationToken);

    public Task<Miembro?> GetByIdAsync(int negocioId, int id, CancellationToken cancellationToken = default) =>
        dbContext.Miembros.SingleOrDefaultAsync(m => m.NegocioId == negocioId && m.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Miembro>> BuscarAsync(
        int negocioId, IReadOnlyList<string> palabrasNormalizadas, CancellationToken cancellationToken = default)
    {
        var consulta = dbContext.Miembros.Where(m => m.NegocioId == negocioId);

        // AND across query words — FUNCTIONAL-SPEC §4's own example ("gomez ana" finds
        // "Ana María Gómez") needs every typed word present, not merely one of them.
        foreach (var palabra in palabrasNormalizadas)
        {
            consulta = consulta.Where(m => m.NombreNormalizado.Contains(palabra));
        }

        return await consulta
            .OrderBy(m => m.Nombre)
            .Take(MaxResultadosBusqueda)
            .ToListAsync(cancellationToken);
    }

    public async Task<Miembro> AddAsync(Miembro miembro, CancellationToken cancellationToken = default)
    {
        dbContext.Miembros.Add(miembro);
        await dbContext.SaveChangesAsync(cancellationToken);
        return miembro;
    }
}
