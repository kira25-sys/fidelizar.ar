using Fidelizar.Domain.Entities;

namespace Fidelizar.Domain.Repositories;

/// <summary>
/// Branches (DATA-MODEL §1). <c>NegocioId</c> is a required parameter on every member (I8).
/// Minimal on purpose, same as every other repository here (ARCHITECTURE §3): S10 only needs a
/// lookup, a listing and a create — there is no update or delete because nothing in phase 1 asks
/// for one, and inventing one is out of scope.
/// </summary>
public interface ISucursalRepository
{
    Task<Sucursal?> GetByIdAsync(int negocioId, int id, CancellationToken cancellationToken = default);

    /// <summary>S10 Usuarios y sucursales — the one legitimate "list" in this product: it is the
    /// owner's own configuration screen, not a socios listing (FUNCTIONAL-SPEC §4's ban is about
    /// members, never about branches).</summary>
    Task<IReadOnlyList<Sucursal>> ListarAsync(int negocioId, CancellationToken cancellationToken = default);

    Task<Sucursal> AddAsync(Sucursal sucursal, CancellationToken cancellationToken = default);
}
