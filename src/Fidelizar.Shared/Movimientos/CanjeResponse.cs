namespace Fidelizar.Shared.Movimientos;

/// <summary>S4 — confirmation shown right after registering a redemption (FUNCTIONAL-SPEC §6):
/// "the new balance is shown immediately".</summary>
public sealed record CanjeResponse(long MovimientoId, decimal Monto, DateOnly FechaEfectiva, decimal SaldoResultante);
