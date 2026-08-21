using System.Globalization;
using System.Text;
using Fidelizar.Shared.Sucursales;

namespace Fidelizar.Client.Formatting;

/// <summary>
/// S9's export (FUNCTIONAL-SPEC §9, "Exportable"): one branch's canjes for one day, as a CSV the
/// manager opens in a spreadsheet — the file that replaces the Trello card she keeps by hand.
/// Written by hand, no library.
/// </summary>
public static class CierreDiarioCsv
{
    /// <summary>";" and not ",": in es-AR the comma is the decimal separator, so a comma-separated
    /// file collapses into one column in Excel.</summary>
    private const char Separador = ';';

    private const string FinDeLinea = "\r\n";

    /// <summary>Excel reads a UTF-8 file without a BOM as ANSI and mangles every accent.</summary>
    public const string Bom = "﻿";

    /// <summary>The exported file, BOM included. <paramref name="sucursal"/> is the branch's name
    /// when the caller knows it, its id otherwise.</summary>
    public static string Construir(CierreDiarioResponse cierre, string sucursal)
    {
        ArgumentNullException.ThrowIfNull(cierre);

        var sb = new StringBuilder(Bom);

        // A saved file has to say what it is: without branch and date it cannot be filed.
        sb.Append("Cierre diario de canjes").Append(FinDeLinea);
        Fila(sb, "Sucursal", sucursal);
        Fila(sb, "Fecha", FechaFormatter.Corta(cierre.Fecha));
        Fila(sb, "Canjes", cierre.Movimientos.Count.ToString(CultureInfo.InvariantCulture));
        sb.Append(FinDeLinea);

        Fila(sb, "Socio", "Monto", "Cajero", "Hora de registro", "Motivo");
        foreach (var movimiento in cierre.Movimientos)
        {
            Fila(
                sb,
                movimiento.MiembroNombre,
                Numero(movimiento.Monto),
                movimiento.CajeroNombre,
                FechaFormatter.ConHora(movimiento.Hora),
                movimiento.Motivo ?? string.Empty);
        }

        // Totals at the foot, as the screen shows them (FUNCTIONAL-SPEC §9).
        sb.Append(FinDeLinea);
        Fila(sb, "Total canjeado", Numero(cierre.TotalCanjeado));

        return sb.ToString();
    }

    /// <summary>The branch id, not its name: a name carries spaces and accents a file name should not.</summary>
    public static string NombreArchivo(CierreDiarioResponse cierre)
    {
        ArgumentNullException.ThrowIfNull(cierre);

        var fecha = cierre.Fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        return $"cierre-diario-sucursal-{cierre.SucursalId}-{fecha}.csv";
    }

    private static void Fila(StringBuilder sb, params string[] campos)
    {
        for (var i = 0; i < campos.Length; i++)
        {
            if (i > 0)
            {
                sb.Append(Separador);
            }

            sb.Append(Escapar(campos[i]));
        }

        sb.Append(FinDeLinea);
    }

    /// <summary>
    /// RFC 4180 quoting, plus the leading apostrophe a spreadsheet needs: a Motivo typed at the
    /// counter is free text, and a cell that starts with "=" is a formula, not a reason.
    /// </summary>
    private static string Escapar(string campo)
    {
        var valor = campo;

        if (EsFormula(valor))
        {
            valor = "'" + valor;
        }

        if (valor.Contains(Separador) || valor.Contains('"') || valor.Contains('\n') || valor.Contains('\r'))
        {
            valor = '"' + valor.Replace("\"", "\"\"", StringComparison.Ordinal) + '"';
        }

        return valor;
    }

    /// <summary>The four leading characters a spreadsheet reads as a formula. "-" only when what
    /// follows is not a digit, so a negative amount stays a number instead of becoming text.</summary>
    private static bool EsFormula(string valor) =>
        valor.Length > 0
        && (valor[0] is '=' or '+' or '@'
            || (valor[0] == '-' && (valor.Length == 1 || !char.IsAsciiDigit(valor[1]))));

    /// <summary>
    /// A number, not the "$ 1.500,50" of <see cref="MoneyFormatter"/>: a cell the manager can add
    /// up. Two decimals and a comma, which is what es-AR Excel parses as a number.
    /// </summary>
    private static string Numero(decimal monto) =>
        monto.ToString("0.00", CultureInfo.InvariantCulture).Replace('.', ',');
}
