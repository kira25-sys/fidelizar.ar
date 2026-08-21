using Fidelizar.Domain.Entities;

namespace Fidelizar.Domain.Repositories;

/// <summary>
/// The credit ledger. Deliberately exposes no Update and no Delete (I1, ARCHITECTURE §3): the
/// table is append-only and every correction is a new <c>Ajuste</c>. A reflection test asserts
/// this interface never grows a method whose name suggests deletion or update. <c>NegocioId</c>
/// is a required parameter on every member (I8).
/// </summary>
public interface IMovimientoRepository
{
    /// <summary>The member's balance: <c>SUM(Monto)</c>. Never read from a stored column (I2).</summary>
    Task<decimal> GetSaldoAsync(int negocioId, int miembroId, CancellationToken cancellationToken = default);

    /// <summary>A member's movements, newest first.</summary>
    Task<IReadOnlyList<MovimientoCredito>> GetPorMiembroAsync(
        int negocioId, int miembroId, CancellationToken cancellationToken = default);

    /// <summary>Every movement for a given <c>Periodo</c> (<c>YYYY-MM</c>) across the business.</summary>
    Task<IReadOnlyList<MovimientoCredito>> GetPorPeriodoAsync(
        int negocioId, string periodo, CancellationToken cancellationToken = default);

    /// <summary>Whether the member has any ledger row at all. The padron importer uses it to
    /// avoid writing a second <c>SaldoInicial</c> on a re-run.</summary>
    Task<bool> TieneMovimientosAsync(int negocioId, int miembroId, CancellationToken cancellationToken = default);

    /// <summary>
    /// A single movement by id, or null. S8 Anular movimiento needs this lookup before it can
    /// write the correcting <c>Ajuste</c> — it does not exist to support editing or deleting
    /// anything (I1): read-only, exactly like every other method on this interface.
    /// </summary>
    Task<MovimientoCredito?> GetByIdAsync(int negocioId, long id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Every movement of one <paramref name="tipo"/> on one calendar day, across the whole
    /// business. S9 Cierre diario uses this to gather a day's <c>Canje</c> rows before narrowing
    /// them to one branch — branch is not a ledger column (DATA-MODEL §4), so that narrowing
    /// happens one layer up, in Application, against the acting cashier's <c>Usuario.SucursalId</c>.
    /// </summary>
    Task<IReadOnlyList<MovimientoCredito>> GetPorFechaEfectivaYTipoAsync(
        int negocioId, DateOnly fechaEfectiva, TipoMovimientoCredito tipo, CancellationToken cancellationToken = default);

    /// <summary>
    /// The movement already recorded under this idempotency key, or null when none exists yet
    /// (README decision #6, 2026-08-19). Every use case that writes on a client's POST — S4's
    /// canje and S8's anulación — calls this before writing anything, so a lost-response retry
    /// with the same key returns the original movement instead of writing a second one.
    /// </summary>
    Task<MovimientoCredito?> GetPorClaveIdempotenciaAsync(
        int negocioId, string claveIdempotencia, CancellationToken cancellationToken = default);

    /// <summary>
    /// Appends one movement. Stamps <see cref="MovimientoCredito.SaldoResultante"/> from the
    /// current balance inside the same transaction as the insert (DATA-MODEL §4), so a concurrent
    /// append can never race past it.
    /// </summary>
    /// <exception cref="Fidelizar.Domain.Exceptions.ConflictException">
    /// <paramref name="movimiento"/> carries a <see cref="MovimientoCredito.ClaveIdempotencia"/>
    /// that another request inserted first, in the instant between this call's own
    /// <see cref="GetPorClaveIdempotenciaAsync"/> check and this insert — the unique partial index
    /// on <c>(NegocioId, ClaveIdempotencia)</c> is what actually closes that race
    /// (<c>CLAVE_IDEMPOTENCIA_EN_USO</c>); the caller re-reads the winner and returns it instead of
    /// surfacing this as a failure.
    /// </exception>
    Task<MovimientoCredito> AppendAsync(MovimientoCredito movimiento, CancellationToken cancellationToken = default);
}
