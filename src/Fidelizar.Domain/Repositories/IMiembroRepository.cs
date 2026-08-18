using Fidelizar.Domain.Entities;

namespace Fidelizar.Domain.Repositories;

/// <summary>
/// Members. <c>NegocioId</c> is a required parameter on every member (I8), so there is nowhere to
/// forget the tenant filter. Minimal on purpose — search and reactivation land in phase 1 as
/// their own methods here, never as a generic <c>GetAll&lt;T&gt;</c> (ARCHITECTURE §3).
/// </summary>
public interface IMiembroRepository
{
    /// <summary>
    /// The member linked to this POS id, or null when none is linked yet. Matches the partial
    /// unique index on <c>(NegocioId, ClienteExternoId)</c> (DATA-MODEL §3); there is no
    /// equivalent lookup for unlinked members, because <c>null</c> is not a key.
    /// </summary>
    Task<Miembro?> GetByClienteExternoIdAsync(
        int negocioId, string clienteExternoId, CancellationToken cancellationToken = default);

    Task<Miembro> AddAsync(Miembro miembro, CancellationToken cancellationToken = default);
}
