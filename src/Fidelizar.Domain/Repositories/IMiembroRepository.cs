using Fidelizar.Domain.Entities;

namespace Fidelizar.Domain.Repositories;

/// <summary>
/// Members. <c>NegocioId</c> is a required parameter on every member, not a convention (I8,
/// ARCHITECTURE §3): there is nowhere for a caller to "forget" the tenant filter.
///
/// This is intentionally minimal — only what the padron importer (F0-08) needs. Search by name
/// and reactivation land later (phase 1, the "socios sin vincular" list — F1-14) as their own
/// methods on this same interface, never as a generic <c>GetAll&lt;T&gt;</c> (ARCHITECTURE §3).
/// </summary>
public interface IMiembroRepository
{
    /// <summary>
    /// The member linked to this POS id within the business, or null when none is linked yet.
    /// Matches the partial unique index on <c>(NegocioId, ClienteExternoId)</c> (DATA-MODEL §3) —
    /// only meaningful for a non-null <paramref name="clienteExternoId"/>; there is no equivalent
    /// lookup for unlinked members, because <c>null</c> is not a key.
    /// </summary>
    Task<Miembro?> GetByClienteExternoIdAsync(
        int negocioId, string clienteExternoId, CancellationToken cancellationToken = default);

    Task<Miembro> AddAsync(Miembro miembro, CancellationToken cancellationToken = default);
}
