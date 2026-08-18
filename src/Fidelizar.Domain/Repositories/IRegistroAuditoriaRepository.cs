using Fidelizar.Domain.Entities;

namespace Fidelizar.Domain.Repositories;

/// <summary>
/// The audit trail (DATA-MODEL §2). Append-only, like <see cref="IMovimientoRepository"/>. No
/// read method yet — the features that write here (F1-08, F1-11, Soporte access) land later.
/// </summary>
public interface IRegistroAuditoriaRepository
{
    Task<RegistroAuditoria> RegistrarAsync(RegistroAuditoria registro, CancellationToken cancellationToken = default);
}
