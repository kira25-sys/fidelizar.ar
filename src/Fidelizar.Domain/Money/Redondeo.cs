namespace Fidelizar.Domain.Money;

/// <summary>
/// I4: money is always <c>decimal</c>, never <c>double</c>, never <c>float</c>, and rounding
/// happens in <b>exactly one place</b> — 2 decimals, <see cref="MidpointRounding.AwayFromZero"/>
/// — so the result matches what a person gets computing it by hand on the spreadsheet the
/// system is verified against.
///
/// Every future computation that produces a monetary amount from a non-money input (RN-01's
/// accrual percentage, in F2-04; a grace-streak threshold check, in F4-04) must round through
/// <see cref="Monto"/> and nowhere else. <c>Fidelizar.Domain.Tests</c> asserts by scanning the
/// source tree that no other <c>Math.Round</c> call exists in the solution — see
/// <c>RedondeoTests.El_redondeo_ocurre_en_un_solo_lugar</c>.
/// </summary>
public static class Redondeo
{
    public const int Decimales = 2;

    public static decimal Monto(decimal valor) =>
        Math.Round(valor, Decimales, MidpointRounding.AwayFromZero);
}
