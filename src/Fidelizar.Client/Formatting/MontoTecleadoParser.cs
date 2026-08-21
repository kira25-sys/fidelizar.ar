using System.Globalization;

namespace Fidelizar.Client.Formatting;

/// <summary>
/// Parses an amount a cashier typed, in hand-typed mode (FUNCTIONAL-SPEC §6): River Plate
/// convention, so "1.500" is 1500 and "1,500" is 1.5.
///
/// <para>
/// Deliberately a second implementation of <c>Fidelizar.Domain.Money.MontoParser.TryParseMontoManual</c>:
/// Client references only Shared (ARCHITECTURE §3) and Domain is out of reach. The two are pinned
/// together by <c>MontoTecleadoParserTests</c>, which runs both over the same table and fails if
/// they ever disagree.
/// </para>
/// </summary>
public static class MontoTecleadoParser
{
    /// <summary>True when <paramref name="texto"/> is a readable amount; the value lands in
    /// <paramref name="monto"/>. Says nothing about the amount being positive or within the
    /// balance — that is the caller's check (I6/RN-24).</summary>
    public static bool TryParse(string? texto, out decimal monto)
    {
        monto = 0m;

        if (string.IsNullOrWhiteSpace(texto))
        {
            return false;
        }

        var limpio = new string(texto.Where(c => c != '$' && !char.IsWhiteSpace(c)).ToArray());

        var negativo = limpio.StartsWith('-');
        if (negativo)
        {
            limpio = limpio[1..];
        }

        if (limpio.Length == 0 || !limpio.All(c => char.IsAsciiDigit(c) || c is '.' or ','))
        {
            return false;
        }

        var punto = limpio.LastIndexOf('.');
        var coma = limpio.LastIndexOf(',');
        string normalizado;

        if (punto >= 0 && coma >= 0)
        {
            // Both kinds appear: the rightmost is the decimal separator.
            normalizado = coma > punto
                ? limpio.Replace(".", string.Empty).Replace(',', '.')
                : limpio.Replace(",", string.Empty);
        }
        else if (punto >= 0 || coma >= 0)
        {
            var separador = punto >= 0 ? '.' : ',';

            if (limpio.Count(c => c == separador) > 1)
            {
                // Repeated: can only be thousands ("1.234.567").
                normalizado = limpio.Replace(separador.ToString(), string.Empty);
            }
            else
            {
                var indice = separador == '.' ? punto : coma;
                var digitosDerecha = limpio.Length - indice - 1;

                normalizado = digitosDerecha != 3
                    // Any count other than 3 is unambiguously decimal, whichever separator it is.
                    ? (separador == '.' ? limpio : limpio.Replace(',', '.'))
                    // Exactly 3 is the ambiguous case: a person typed it, so dot = thousands,
                    // comma = decimal.
                    : (separador == '.' ? limpio.Replace(".", string.Empty) : limpio.Replace(',', '.'));
            }
        }
        else
        {
            normalizado = limpio;
        }

        if (negativo)
        {
            normalizado = "-" + normalizado;
        }

        return decimal.TryParse(
            normalizado,
            NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
            CultureInfo.InvariantCulture,
            out monto);
    }
}
