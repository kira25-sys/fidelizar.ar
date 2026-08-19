namespace Fidelizar.Shared.Sucursales;

/// <summary>One redemption row in S9's daily close.</summary>
public sealed record CierreDiarioMovimiento(
    string MiembroNombre, decimal Monto, string CajeroNombre, DateTime Hora, string? Motivo);

/// <summary>S9 Cierre diario de canjes (Encargada/Dueño only) — one branch's redemptions for one
/// day: member, amount, cashier, time, reason, with totals at the foot (FUNCTIONAL-SPEC §9).</summary>
public sealed record CierreDiarioResponse(
    int SucursalId, DateOnly Fecha, decimal TotalCanjeado, IReadOnlyList<CierreDiarioMovimiento> Movimientos);
