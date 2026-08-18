using Fidelizar.Domain.Entities;
using Fidelizar.Domain.Repositories;
using Fidelizar.Infrastructure.Persistence;

namespace Fidelizar.Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IRegistroAuditoriaRepository"/>. Insert-only, like
/// <see cref="MovimientoRepository"/> — there is no method here that could ever issue an Update or
/// a Delete against <c>RegistrosAuditoria</c>.
/// </summary>
public sealed class RegistroAuditoriaRepository(FidelizarDbContext dbContext) : IRegistroAuditoriaRepository
{
    public async Task<RegistroAuditoria> RegistrarAsync(
        RegistroAuditoria registro, CancellationToken cancellationToken = default)
    {
        dbContext.RegistrosAuditoria.Add(registro);
        await dbContext.SaveChangesAsync(cancellationToken);
        return registro;
    }
}
