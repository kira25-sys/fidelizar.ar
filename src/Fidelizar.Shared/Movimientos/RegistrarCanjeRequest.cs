using System.ComponentModel.DataAnnotations;

namespace Fidelizar.Shared.Movimientos;

/// <summary>
/// S4 Registrar canje request body (FUNCTIONAL-SPEC §6). <c>Monto</c> is what
/// <c>MontoParser</c> already resolved to a number client-side — this attribute only rejects
/// zero or negative as a fast hint; I6/RN-24 (never above the balance) is enforced server-side by
/// <c>SaldoService</c>, which is the only place that knows the balance.
///
/// <c>Motivo</c> is marked required here because <c>MovimientoCredito.Crear</c> currently
/// requires it for every <c>Canje</c> regardless of date — FUNCTIONAL-SPEC §6 describes it as
/// optional when <c>FechaEfectiva</c> is today, which disagrees with that Domain rule. Flagged in
/// docs/REST-CONTRACT-F1.md; not resolved here.
/// </summary>
public sealed record RegistrarCanjeRequest(
    [Range(typeof(decimal), "0.01", "79228162514264337593543950335", ErrorMessage = "El monto tiene que ser mayor a cero.")]
    decimal Monto,
    DateOnly FechaEfectiva,
    [Required(ErrorMessage = "El motivo es obligatorio.")]
    string Motivo);
