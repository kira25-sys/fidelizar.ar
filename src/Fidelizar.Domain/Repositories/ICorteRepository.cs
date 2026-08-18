using Fidelizar.Domain.Entities;

namespace Fidelizar.Domain.Repositories;

/// <summary>
/// The program's cutoff, one row per business, unique at the schema level (DATA-MODEL §4).
/// <c>NegocioId</c> is a required parameter on every member (I8).
/// </summary>
public interface ICorteRepository
{
    /// <summary>The business's cutoff, or null when none has been declared yet (F0-07) — absence
    /// is a legitimate state the caller must handle, never invented.</summary>
    Task<Corte?> ObtenerAsync(int negocioId, CancellationToken cancellationToken = default);

    /// <summary>Persists a newly declared cutoff. A second one is rejected by the unique index on
    /// <c>NegocioId</c>.</summary>
    Task<Corte> DeclararAsync(Corte corte, CancellationToken cancellationToken = default);
}
