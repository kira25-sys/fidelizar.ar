using Fidelizar.Domain.Entities;

namespace Fidelizar.Application.Services;

/// <summary>What the caller supplies to register a redemption (RN-05, RN-24).</summary>
/// <param name="NegocioId">Required, not a convention (I8).</param>
/// <param name="MiembroId">The member redeeming.</param>
/// <param name="Monto">Positive. The service stores it as a negative movement.</param>
/// <param name="Motivo">Mandatory for every <c>Canje</c> (DATA-MODEL §4).</param>
/// <param name="UsuarioId">Who is registering it. Null only for <c>sistema</c>.</param>
/// <param name="FechaEfectiva">When the redemption actually happened — may be in the past
/// (a power-cut redemption written on paper and loaded later, ARCHITECTURE §10).</param>
/// <param name="Hoy">Today's date, used to decide whether <paramref name="FechaEfectiva"/> is
/// retroactive. Passed in rather than read from the clock so this is testable without one.</param>
public sealed record RegistrarCanjeRequest(
    int NegocioId,
    int MiembroId,
    decimal Monto,
    string Motivo,
    int? UsuarioId,
    DateOnly FechaEfectiva,
    DateOnly Hoy);

public interface ISaldoService
{
    /// <summary>The member's balance: <c>SUM(Monto)</c> of their movements (I2).</summary>
    Task<decimal> ObtenerSaldoAsync(int negocioId, int miembroId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Registers a redemption. Rejects any amount above the available balance (I6, RN-24) and
    /// blocks every redemption outright while the balance is negative (RN-25) — that state is
    /// recovered only by a system-generated <c>Ajuste</c>, never by a human redemption.
    /// </summary>
    Task<MovimientoCredito> RegistrarCanjeAsync(
        RegistrarCanjeRequest request, CancellationToken cancellationToken = default);
}
