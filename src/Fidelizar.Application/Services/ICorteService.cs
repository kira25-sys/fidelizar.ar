using Fidelizar.Domain.Entities;

namespace Fidelizar.Application.Services;

/// <summary>
/// The program's cutoff for one business (F0-07, DATA-MODEL §4). Declared at import, persisted,
/// never invented.
/// </summary>
public interface ICorteService
{
    /// <summary>
    /// The business's current cutoff, or throws when none has been declared. Accrual (F2-04)
    /// must call this — and only this — before crediting anything: with no cutoff on record, a
    /// hard-coded or guessed date would double-credit every purchase between the real cutoff and
    /// the import day, exactly as it once did in Octaviano. Failing loudly here is the whole
    /// point of F0-07.
    /// </summary>
    /// <exception cref="Fidelizar.Domain.Exceptions.ConflictException">
    /// No <see cref="Corte"/> is on record for <paramref name="negocioId"/>.
    /// </exception>
    Task<Corte> ObtenerCorteVigenteAsync(int negocioId, CancellationToken cancellationToken = default);

    /// <summary>Declares the business's cutoff. The unique index on <c>NegocioId</c> (F0-03)
    /// rejects declaring a second one at the database level.</summary>
    Task<Corte> DeclararCorteAsync(
        int negocioId, DateOnly fecha, int declaradoPorUsuarioId, CancellationToken cancellationToken = default);
}
