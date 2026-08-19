using Fidelizar.Domain.Entities;

namespace Fidelizar.Application.Services;

/// <param name="NegocioId">Required, not a convention (I8).</param>
/// <param name="MovimientoId">The movement being corrected.</param>
/// <param name="Motivo">Mandatory — every <c>Ajuste</c> needs one (I3).</param>
/// <param name="UsuarioId">Who is voiding it. Required: an <c>Ajuste</c> always has a real actor
/// (Encargada/Dueño only, FUNCTIONAL-SPEC §8).</param>
/// <param name="Hoy">When the void itself happens — the new <c>Ajuste</c> row's own
/// <c>FechaEfectiva</c>, independent of the original movement's date.</param>
public sealed record AnularMovimientoRequest(int NegocioId, long MovimientoId, string Motivo, int UsuarioId, DateOnly Hoy);

/// <summary>
/// S8 Anular movimiento (FUNCTIONAL-SPEC §8) — the UI surface of I1/I3. There is no edit and no
/// delete anywhere in this product: voiding movement <c>M</c> writes a new <c>Ajuste</c> of
/// <c>-M.Monto</c>. Both rows stay in the ledger forever.
/// </summary>
public interface IAnulacionMovimientoService
{
    /// <exception cref="Fidelizar.Domain.Exceptions.EntityNotFoundException">
    /// No movement with that id exists for this business — including one that belongs to a
    /// different <c>NegocioId</c> (I8: never distinguished from "does not exist" in the response).
    /// </exception>
    Task<MovimientoCredito> AnularAsync(
        AnularMovimientoRequest request, CancellationToken cancellationToken = default);
}
