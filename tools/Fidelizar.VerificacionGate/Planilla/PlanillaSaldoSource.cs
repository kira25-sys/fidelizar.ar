using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using Fidelizar.VerificacionGate.Comparacion;

namespace Fidelizar.VerificacionGate.Planilla;

/// <summary>
/// Punta 3 (ROADMAP F0-11): the owner's spreadsheet, read-only via ClosedXML — already
/// owner-authorized for <c>Fidelizar.Infrastructure</c> (F0-08).
///
/// <para>
/// <b>The real file is not the flat, single-sheet shape <c>VipPadronImporter</c> expects.</b> It
/// is one worksheet per branch (five branches), each with a title row, a blank row, and the real
/// column header on row 4 — plus a "Resumen" worksheet that aggregates by branch (no per-member id
/// column at all) and an "A recordar" worksheet of follow-up notes (no per-member id column
/// either). Both of those, and any other non-roster worksheet, are excluded automatically: a
/// worksheet only counts as a member roster if one of its first ten rows contains a cell matching
/// a client-id alias below — <c>BuscarFilaEncabezado</c> finds that row wherever it actually is,
/// rather than assuming row 1.
/// </para>
/// </summary>
public static class PlanillaSaldoSource
{
    // Headers matched normalised (lowercase, letters/digits only, collapsed whitespace) so
    // "Cliente Nº", "cliente nº" and "CLIENTE Nº" all land on the same alias. The real header in
    // every branch sheet is literally "Cliente Nº" — kept first, most specific alias wins.
    private static readonly string[] AliasClienteId =
        ["cliente nº", "cliente n", "cliente id", "customer_id", "customerid", "id cliente", "idcliente", "id del cliente", "id"];

    private static readonly string[] AliasCredito =
        ["total", "credito", "credito actual", "saldo", "saldo actual", "puntos", "total de puntos"];

    public static (IReadOnlyList<FilaPlanilla> Filas, IReadOnlyList<string> HojasIgnoradas) Leer(string rutaExcel)
    {
        // FileShare.ReadWrite: the owner's file is routinely open in a spreadsheet program while
        // this runs. This still opens for READ only — no write handle is ever requested.
        using var stream = new FileStream(rutaExcel, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var workbook = new XLWorkbook(stream);

        var filas = new List<FilaPlanilla>();
        var hojasIgnoradas = new List<string>();

        foreach (var worksheet in workbook.Worksheets)
        {
            var encabezado = BuscarFilaEncabezado(worksheet);
            if (encabezado is null)
            {
                hojasIgnoradas.Add($"{worksheet.Name} (no tiene una columna de id de cliente reconocible — no es una hoja de socios)");
                continue;
            }

            var (filaEncabezado, colId, colCredito) = encabezado.Value;

            if (colCredito is null)
            {
                hojasIgnoradas.Add($"{worksheet.Name} (tiene columna de id pero no de crédito reconocible)");
                continue;
            }

            var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? filaEncabezado;
            var lastColumn = worksheet.LastColumnUsed()?.ColumnNumber() ?? Math.Max(colId!.Value, colCredito.Value);

            for (var numeroFila = filaEncabezado + 1; numeroFila <= lastRow; numeroFila++)
            {
                var row = worksheet.Row(numeroFila);
                if (FilaVacia(row, lastColumn))
                {
                    continue;
                }

                var clienteExternoId = LeerIdEntero(row.Cell(colId!.Value));
                var credito = LeerDecimalCelda(row.Cell(colCredito.Value)) ?? 0m;

                filas.Add(new FilaPlanilla(worksheet.Name, numeroFila, clienteExternoId, credito));
            }
        }

        return (filas, hojasIgnoradas);
    }

    private static (int Fila, int? ColId, int? ColCredito)? BuscarFilaEncabezado(IXLWorksheet worksheet)
    {
        var lastRow = Math.Min(worksheet.LastRowUsed()?.RowNumber() ?? 1, 10);
        var lastColumn = worksheet.LastColumnUsed()?.ColumnNumber() ?? 1;

        for (var fila = 1; fila <= lastRow; fila++)
        {
            var row = worksheet.Row(fila);
            var mapa = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var col = 1; col <= lastColumn; col++)
            {
                var texto = Normalizar(row.Cell(col).GetString());
                if (texto.Length > 0 && !mapa.ContainsKey(texto))
                {
                    mapa[texto] = col;
                }
            }

            var colId = Buscar(mapa, AliasClienteId);
            if (colId is null)
            {
                continue;
            }

            var colCredito = Buscar(mapa, AliasCredito);
            return (fila, colId, colCredito);
        }

        return null;
    }

    private static int? Buscar(Dictionary<string, int> mapa, string[] alias)
    {
        foreach (var a in alias)
        {
            if (mapa.TryGetValue(a, out var col))
            {
                return col;
            }
        }

        return null;
    }

    private static string Normalizar(string texto)
    {
        var minuscula = texto.Trim().ToLowerInvariant();
        var limpio = new StringBuilder(minuscula.Length);
        foreach (var c in minuscula)
        {
            limpio.Append(char.IsLetterOrDigit(c) ? c : ' ');
        }

        return string.Join(' ', limpio.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static bool FilaVacia(IXLRow row, int lastColumn)
    {
        for (var col = 1; col <= lastColumn; col++)
        {
            if (!string.IsNullOrWhiteSpace(row.Cell(col).GetString()))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Blank is a legitimate id: it means the row could not be matched to a member (a
    /// sheet total, a footer, a stray note) — never guessed, always reported by the caller.</summary>
    private static string? LeerIdEntero(IXLCell cell)
    {
        if (cell.IsEmpty())
        {
            return null;
        }

        var texto = cell.GetString().Trim();
        if (texto.Length == 0)
        {
            return null;
        }

        // Excel can return an integer cell's text as "4156" or "4156.0" depending on the cell's
        // own format — parsed as decimal first, then required to be a whole number, exactly like
        // VipPadronImporter's own TryParseEntero.
        if (!decimal.TryParse(texto, NumberStyles.Number, CultureInfo.InvariantCulture, out var valor) ||
            valor != Math.Floor(valor))
        {
            return null;
        }

        return ((long)valor).ToString(CultureInfo.InvariantCulture);
    }

    private static decimal? LeerDecimalCelda(IXLCell cell)
    {
        if (cell.IsEmpty())
        {
            return null;
        }

        if (cell.TryGetValue<decimal>(out var valorNumerico))
        {
            return valorNumerico;
        }

        var texto = cell.GetString().Trim();
        return decimal.TryParse(texto, NumberStyles.Number | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var valor)
            ? valor
            : null;
    }
}
