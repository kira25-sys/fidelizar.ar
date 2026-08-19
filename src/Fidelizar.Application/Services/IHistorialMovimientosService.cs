using Fidelizar.Domain.Entities;

namespace Fidelizar.Application.Services;

/// <summary>One ledger row, with the acting user's name resolved (S7/S8's shared shape). Never
/// exposes anything the ledger row itself does not already carry — no new data, just the join
/// <c>MovimientoCredito.UsuarioId</c> alone cannot give a controller.</summary>
public sealed record MovimientoHistorialItem(
    long Id,
    TipoMovimientoCredito Tipo,
    decimal Monto,
    DateOnly FechaEfectiva,
    DateTime RegistradoEn,
    string? Motivo,
    decimal SaldoResultante,
    string? UsuarioNombre);

/// <summary>
/// S7 Historial de movimientos (Encargada/Dueño only). Wraps
/// <see cref="Fidelizar.Domain.Repositories.IMovimientoRepository.GetPorMiembroAsync"/> — the
/// repository call already existed; what was missing was this use case, so a controller does not
/// call the repository directly (ARCHITECTURE §3).
/// </summary>
public interface IHistorialMovimientosService
{
    /// <exception cref="Fidelizar.Domain.Exceptions.EntityNotFoundException">
    /// No member with <paramref name="miembroId"/> exists for this business.
    /// </exception>
    Task<IReadOnlyList<MovimientoHistorialItem>> ObtenerAsync(
        int negocioId, int miembroId, CancellationToken cancellationToken = default);
}
