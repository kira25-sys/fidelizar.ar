namespace Fidelizar.Client.Formatting;

/// <summary>
/// How a ledger row is named on screen. Shared by S7's table and S8's confirm dialog so the
/// movement being voided reads exactly as the row it was clicked from.
/// </summary>
public static class MovimientoFormatter
{
    /// <summary>The enum's own name, as it travels on the wire. The fallback is deliberate:
    /// <c>Tipo</c> is append-only (DATA-MODEL §4), so a type added later shows up under its raw
    /// name instead of vanishing from a ledger row.</summary>
    public static string EtiquetaTipo(string tipo) => tipo switch
    {
        "SaldoInicial" => "Saldo inicial",
        "Acumulacion" => "Acumulación",
        "Canje" => "Canje",
        "Ajuste" => "Ajuste",
        _ => tipo,
    };

    public static string ClaseBadgeTipo(string tipo) => tipo switch
    {
        "Acumulacion" => "badge--success",
        "Ajuste" => "badge--warning",
        _ => "badge--neutral",
    };

    /// <summary>An explicit "+" so the direction of a movement reads without comparing it to the
    /// row above (the "−" comes from MoneyFormatter itself). The badge beside it carries the same
    /// information in words: colour never says anything on its own (DESIGN-SYSTEM §4).</summary>
    public static string MontoConSigno(decimal monto) =>
        monto > 0 ? $"+{MoneyFormatter.Format(monto)}" : MoneyFormatter.Format(monto);

    /// <summary>
    /// The amount the correcting <c>Ajuste</c> will carry: the exact opposite of the original
    /// (FUNCTIONAL-SPEC §8, I1). Shown before confirming so nobody has to work out the sign in
    /// their head — the server computes its own, this never decides anything.
    /// </summary>
    public static decimal MontoDelAjuste(decimal montoOriginal) => -montoOriginal;
}
