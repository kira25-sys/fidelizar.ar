using Fidelizar.Domain.Entities;

namespace Fidelizar.Domain.Repositories;

/// <summary>
/// The credit ledger. Deliberately <b>exposes no Update and no Delete</b> (I1, ARCHITECTURE §3):
/// the table is append-only and every correction is a new <c>Ajuste</c> movement, never an edit
/// to an existing row. <c>Fidelizar.Domain.Tests</c> asserts by reflection that this interface —
/// and every repository interface in this namespace — never grows a method whose name suggests
/// deletion or update over the ledger.
///
/// <c>NegocioId</c> is a required parameter on every member, not a convention (I8, ARCHITECTURE
/// §3): there is nowhere for a caller to "forget" the tenant filter.
/// </summary>
public interface IMovimientoRepository
{
    /// <summary>The member's balance: <c>SUM(Monto)</c> of their movements. Never read from a
    /// stored column (I2).</summary>
    Task<decimal> GetSaldoAsync(int negocioId, int miembroId, CancellationToken cancellationToken = default);

    /// <summary>A member's movements, newest first.</summary>
    Task<IReadOnlyList<MovimientoCredito>> GetPorMiembroAsync(
        int negocioId, int miembroId, CancellationToken cancellationToken = default);

    /// <summary>Every movement for a given <c>Periodo</c> (<c>YYYY-MM</c>) across the business.</summary>
    Task<IReadOnlyList<MovimientoCredito>> GetPorPeriodoAsync(
        int negocioId, string periodo, CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether the member already has any ledger row at all. The padron importer (F0-08) uses
    /// this to avoid writing a second <c>SaldoInicial</c> on a re-run: the initial balance is
    /// written once per member, ever.
    /// </summary>
    Task<bool> TieneMovimientosAsync(int negocioId, int miembroId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Appends one movement to the ledger. Computes and stamps
    /// <see cref="MovimientoCredito.SaldoResultante"/> from the current balance inside the same
    /// transaction as the insert (DATA-MODEL §4), so a concurrent append can never race past it.
    /// </summary>
    Task<MovimientoCredito> AppendAsync(MovimientoCredito movimiento, CancellationToken cancellationToken = default);
}
