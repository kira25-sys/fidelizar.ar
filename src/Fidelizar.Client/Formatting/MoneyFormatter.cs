using System.Globalization;

namespace Fidelizar.Client.Formatting;

/// <summary>
/// "$ 12.400" / "$ 1.500,50" — River Plate convention (FLOW-S2-S5.md "Conventions used
/// below"): "$" then a space, "." as the thousands separator, no decimals for a whole peso
/// amount, ",NN" otherwise. Built by hand rather than via <c>CultureInfo("es-AR")</c>: Blazor
/// WebAssembly's trimmed ICU data does not reliably ship every culture, and this format is
/// fully specified, so a culture lookup buys nothing but a runtime risk.
///
/// Never rounds: I4 already guarantees every amount arrives with at most two decimal places
/// (<c>Fidelizar.Domain.Money.Redondeo</c> is the one place that rounds, and a Domain.Tests
/// invariant enforces that literally — this file must never call the rounding method Redondeo
/// itself calls, or that test fails). This is display-only, so it just reads the decimal's own
/// digits, truncated, never rounded.
/// </summary>
public static class MoneyFormatter
{
    public static string Format(decimal value)
    {
        var integerPart = Math.Truncate(value);
        var centavos = (int)((value - integerPart) * 100);

        var sign = value < 0 ? "-" : string.Empty;
        var grouped = GroupThousands(Math.Abs(integerPart).ToString("F0", CultureInfo.InvariantCulture));
        var decimals = centavos == 0 ? string.Empty : $",{Math.Abs(centavos):D2}";

        return $"{sign}$ {grouped}{decimals}";
    }

    private static string GroupThousands(string digits)
    {
        var chars = new List<char>(digits.Length + digits.Length / 3);
        for (var i = 0; i < digits.Length; i++)
        {
            if (i > 0 && (digits.Length - i) % 3 == 0)
            {
                chars.Add('.');
            }

            chars.Add(digits[i]);
        }

        return new string(chars.ToArray());
    }
}
