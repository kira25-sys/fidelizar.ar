using Fidelizar.Domain.Entities;

namespace Fidelizar.Domain.Repositories;

/// <summary>
/// The audit trail (DATA-MODEL §2). Append-only, like <see cref="IMovimientoRepository"/> — there
/// is no read method here yet, because no F1-03 call site needs one. The entity, the table and
/// this repository exist so the features that actually write to it (F1-08, F1-11, Soporte access,
/// …) have somewhere to write; F1-03 itself does not write here.
/// </summary>
public interface IRegistroAuditoriaRepository
{
    Task<RegistroAuditoria> RegistrarAsync(RegistroAuditoria registro, CancellationToken cancellationToken = default);
}
