namespace Fidelizar.Domain.Money;

/// <summary>
/// I4: money is always <c>decimal</c>, and rounding happens in exactly one place — 2 decimals,
/// <see cref="MidpointRounding.AwayFromZero"/> — so the result matches what a person gets by
/// hand. <c>RedondeoTests.El_redondeo_ocurre_en_un_solo_lugar</c> scans the source tree to assert
/// no other <c>Math.Round</c> call exists in the solution.
/// </summary>
public static class Redondeo
{
    public const int Decimales = 2;

    public static decimal Monto(decimal valor) =>
        Math.Round(valor, Decimales, MidpointRounding.AwayFromZero);
}
