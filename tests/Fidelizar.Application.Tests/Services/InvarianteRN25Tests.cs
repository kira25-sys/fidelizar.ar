namespace Fidelizar.Application.Tests.Services;

/// <summary>
/// I6 / RN-25 — a sale voided after its credit was already redeemed writes the correcting
/// <c>Ajuste</c> anyway (leaving the balance negative), blocks further redemptions for that
/// member, and notifies the manager. The "while negative, every human redemption is blocked
/// outright" half is already covered and active — see
/// <c>SaldoServiceTests.Canje_con_saldo_negativo_se_bloquea</c>, which reaches that state directly
/// by appending the system <c>Ajuste</c> by hand, exactly as RN-25 describes it arising.
///
/// What is NOT testable yet is producing that <c>Ajuste</c> FROM an actual voided sale: there is
/// no <c>Venta</c> entity and no accrual/void engine in this wave (both are F2-04), and there is
/// no manager-notification mechanism at all yet. Inventing either here to turn this green would
/// test code that does not exist in <c>src/</c>.
/// </summary>
public class InvarianteRN25Tests
{
    [Fact(Skip = "Requiere Venta, el motor de acumulación/anulación y notificación a la encargada — F2-04")]
    public void Venta_anulada_despues_de_canjeado_el_credito_escribe_el_Ajuste_igual_y_notifica_a_la_encargada()
    {
    }
}
