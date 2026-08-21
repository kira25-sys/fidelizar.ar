namespace Fidelizar.Client.Formatting;

/// <summary>
/// What S5's birthday pair offers: twelve months and the days each one has (RN-11 — day and
/// month only, so there is no year field to leave blank or fill with a fake one).
///
/// <para>
/// The month names are literals for the same reason <see cref="MoneyFormatter"/>'s format is:
/// Blazor WebAssembly's trimmed ICU data does not reliably ship every culture, and twelve fixed
/// Spanish words are fully specified without one.
/// </para>
/// </summary>
public static class FechaNacimientoOpciones
{
    /// <summary>The twelve months, in calendar order, as the select renders them.</summary>
    public static IReadOnlyList<Mes> Meses { get; } =
    [
        new(1, "enero"),
        new(2, "febrero"),
        new(3, "marzo"),
        new(4, "abril"),
        new(5, "mayo"),
        new(6, "junio"),
        new(7, "julio"),
        new(8, "agosto"),
        new(9, "septiembre"),
        new(10, "octubre"),
        new(11, "noviembre"),
        new(12, "diciembre"),
    ];

    /// <summary>
    /// How many days the day select offers for <paramref name="mes"/>. February gets 29:
    /// <c>AltaMiembroService.ResolverFechaNacimiento</c> stores the pair against the leap year
    /// 2000, so a 29/02 birthday is valid there and has to be pickable here. With no month chosen
    /// yet the select offers 31 — the day may be picked first.
    /// </summary>
    public static int DiasDelMes(int? mes) => mes switch
    {
        2 => 29,
        4 or 6 or 9 or 11 => 30,
        _ => 31,
    };

    /// <summary>
    /// True when <paramref name="dia"/> fits <paramref name="mes"/> — the check the day select
    /// uses to drop a day that no longer exists after the month changed (31 then "febrero").
    /// A null day fits any month: leaving the birthday empty is allowed.
    /// </summary>
    public static bool DiaCabeEnMes(int? dia, int? mes) => dia is null || dia <= DiasDelMes(mes);

    /// <summary>One month as the select renders it: the number the API takes, the word the cashier reads.</summary>
    public sealed record Mes(int Numero, string Nombre);
}
