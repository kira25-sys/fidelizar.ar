namespace Fidelizar.Application.Services;

public sealed record CierreDiarioItem(
    string MiembroNombre, decimal Monto, string CajeroNombre, DateTime Hora, string? Motivo);

public sealed record CierreDiarioResultado(
    int SucursalId, DateOnly Fecha, decimal TotalCanjeado, IReadOnlyList<CierreDiarioItem> Movimientos);

/// <summary>
/// S9 Cierre diario de canjes (Encargada/Dueño only, FUNCTIONAL-SPEC §9) — one branch's
/// redemptions for one day: member, amount, cashier, time, reason. A <c>Canje</c> carries no
/// <c>SucursalId</c> of its own (DATA-MODEL §4 — branch is organisational, RN-07), so "this
/// branch's redemptions" means the redemptions registered by a cashier stationed at it
/// (<c>Usuario.SucursalId</c>), not the member's own branch — RN-07/FUNCTIONAL-SPEC are explicit
/// that a member from another branch is served normally, so filtering by the member's branch
/// would silently drop exactly the cross-branch redemptions this report exists to show the
/// manager.
/// </summary>
public interface ICierreDiarioService
{
    Task<CierreDiarioResultado> ObtenerAsync(
        int negocioId, int sucursalId, DateOnly fecha, CancellationToken cancellationToken = default);
}
