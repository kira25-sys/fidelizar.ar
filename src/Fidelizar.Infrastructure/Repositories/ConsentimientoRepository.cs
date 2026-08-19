using Fidelizar.Domain.Entities;
using Fidelizar.Domain.Repositories;
using Fidelizar.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Fidelizar.Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IConsentimientoRepository"/>. Exposes no Update and no
/// Delete (I1-adjacent discipline for consent) — every method here either reads or appends.
/// </summary>
public sealed class ConsentimientoRepository(FidelizarDbContext dbContext) : IConsentimientoRepository
{
    public Task<Consentimiento?> GetVigenteAsync(
        int negocioId, int miembroId, TipoConsentimiento tipo, CancellationToken cancellationToken = default) =>
        dbContext.Consentimientos
            .Where(c => c.NegocioId == negocioId && c.MiembroId == miembroId && c.Tipo == tipo)
            // Append-only: the newest row is the current one. OcurridoEn breaks ties by real-world
            // time; Id is the final tiebreaker for two rows recorded in the very same instant.
            .OrderByDescending(c => c.OcurridoEn)
            .ThenByDescending(c => c.Id)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<Consentimiento>> GetHistorialAsync(
        int negocioId, int miembroId, CancellationToken cancellationToken = default) =>
        await dbContext.Consentimientos
            .Where(c => c.NegocioId == negocioId && c.MiembroId == miembroId)
            .OrderByDescending(c => c.OcurridoEn)
            .ThenByDescending(c => c.Id)
            .ToListAsync(cancellationToken);

    public async Task<Consentimiento> AppendAsync(
        Consentimiento consentimiento, CancellationToken cancellationToken = default)
    {
        dbContext.Consentimientos.Add(consentimiento);
        await dbContext.SaveChangesAsync(cancellationToken);
        return consentimiento;
    }
}
