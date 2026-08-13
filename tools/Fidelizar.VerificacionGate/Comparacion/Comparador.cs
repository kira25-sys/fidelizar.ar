using Fidelizar.Domain.Money;

namespace Fidelizar.VerificacionGate.Comparacion;

/// <summary>One row read from the owner's spreadsheet (Punta 3), before it is matched against the
/// other two sources.</summary>
public sealed record FilaPlanilla(string Hoja, int NumeroFila, string? ClienteExternoId, decimal Credito);

public enum CausaDiscrepancia
{
    /// <summary>The three sources agree on which member exists, but not on how much they have.</summary>
    MontoDistinto,

    /// <summary>The member appears in Octaviano and/or the spreadsheet but not in Postgres.</summary>
    FaltaEnPostgres,

    /// <summary>The member appears in Postgres and/or the spreadsheet but not in octaviano.db.</summary>
    FaltaEnOctaviano,

    /// <summary>The member appears in Postgres and octaviano.db but not in the spreadsheet.</summary>
    FaltaEnPlanilla,
}

/// <summary>
/// One member whose three balances do not all agree — the whole reason F0-11 exists. Any of the
/// three amounts can be null when the member is entirely absent from that source (see
/// <see cref="CausaDiscrepancia"/>). Identified only by <see cref="ClienteExternoId"/> — CLAUDE.md.
/// </summary>
public sealed record Discrepancia(
    string ClienteExternoId,
    decimal? SaldoPostgres,
    decimal? SaldoOctaviano,
    decimal? SaldoPlanilla,
    CausaDiscrepancia Causa)
{
    /// <summary>The largest gap between any two of the (present) amounts.</summary>
    public decimal DiferenciaMaxima()
    {
        var valores = new[] { SaldoPostgres, SaldoOctaviano, SaldoPlanilla }
            .Where(v => v is not null)
            .Select(v => v!.Value)
            .ToList();

        return valores.Count < 2 ? 0m : valores.Max() - valores.Min();
    }
}

/// <summary>A structural edge case worth reporting even when it is not, by itself, a peso-value
/// discrepancy (e.g. a spreadsheet row with no recognisable id).</summary>
public sealed record CasoBorde(string Descripcion, string? ClienteExternoId = null);

public sealed record ResultadoComparacion(
    int TotalSociosPostgres,
    int TotalSociosOctaviano,
    int TotalFilasPlanillaValidas,
    int TotalSociosComparados,
    IReadOnlyList<Discrepancia> Discrepancias,
    IReadOnlyList<CasoBorde> CasosBorde,
    decimal SumaPostgres,
    decimal SumaOctaviano,
    decimal SumaPlanilla)
{
    /// <summary>ROADMAP F0-11 / ARCHITECTURE §4: the gate is met only when every member's three
    /// balances agree to the peso, with no exceptions and no sampling.</summary>
    public bool GateCumplido => Discrepancias.Count == 0;
}

/// <summary>
/// The three-way comparison itself (ROADMAP F0-11). Pure function: no I/O, no database, no file
/// system — every source is read elsewhere and handed in as plain data, so this class is testable
/// with invented fixtures only (CLAUDE.md: a real member never becomes a test case) and is the
/// part of the harness that F0-15's restore drill re-runs unchanged.
/// </summary>
public static class Comparador
{
    public static ResultadoComparacion Comparar(
        IReadOnlyDictionary<string, decimal> saldosPostgres,
        IReadOnlyDictionary<string, decimal> saldosOctaviano,
        IReadOnlyList<FilaPlanilla> filasPlanilla)
    {
        var casosBorde = new List<CasoBorde>();

        // A row with no recognisable id is not a member row — a sheet total, a footer, a stray
        // note. Reported, never guessed, never silently dropped.
        foreach (var fila in filasPlanilla.Where(f => f.ClienteExternoId is null))
        {
            casosBorde.Add(new CasoBorde(
                $"Planilla, hoja '{fila.Hoja}', fila {fila.NumeroFila}: sin ClienteExternoId " +
                "reconocible (fila de total, nota o encabezado repetido) — no se comparó."));
        }

        // A repeated id inside the spreadsheet is ambiguous — same discipline VipPadronImporter
        // already applies to a repeated customer_id (F0-08): drop every row for that id, let a
        // person resolve it, never guess which row is "the" real one.
        var conId = filasPlanilla.Where(f => f.ClienteExternoId is not null).ToList();
        var duplicados = conId.GroupBy(f => f.ClienteExternoId!).Where(g => g.Count() > 1).ToList();

        foreach (var grupo in duplicados)
        {
            var ubicaciones = string.Join(", ", grupo.Select(f => $"{f.Hoja} fila {f.NumeroFila}"));
            casosBorde.Add(new CasoBorde(
                $"ClienteExternoId {grupo.Key} aparece {grupo.Count()} veces en la planilla " +
                $"({ubicaciones}) — no se comparó, cuál fila es la correcta lo decide una persona.",
                grupo.Key));
        }

        var idsDuplicadosEnPlanilla = duplicados.Select(g => g.Key).ToHashSet();
        var saldosPlanilla = conId
            .Where(f => !idsDuplicadosEnPlanilla.Contains(f.ClienteExternoId!))
            .ToDictionary(f => f.ClienteExternoId!, f => f.Credito);

        var todosLosIds = new SortedSet<string>(StringComparer.Ordinal);
        todosLosIds.UnionWith(saldosPostgres.Keys);
        todosLosIds.UnionWith(saldosOctaviano.Keys);
        todosLosIds.UnionWith(saldosPlanilla.Keys);

        var discrepancias = new List<Discrepancia>();

        foreach (var id in todosLosIds)
        {
            var enPostgres = saldosPostgres.TryGetValue(id, out var saldoPostgres);
            var enOctaviano = saldosOctaviano.TryGetValue(id, out var saldoOctaviano);
            var enPlanilla = saldosPlanilla.TryGetValue(id, out var saldoPlanilla);

            if (!enPostgres)
            {
                casosBorde.Add(new CasoBorde(
                    $"ClienteExternoId {id}: no está en Postgres (sí en octaviano.db: {enOctaviano}, " +
                    $"sí en la planilla: {enPlanilla}).", id));
                discrepancias.Add(new Discrepancia(
                    id, null, enOctaviano ? saldoOctaviano : null, enPlanilla ? saldoPlanilla : null,
                    CausaDiscrepancia.FaltaEnPostgres));
                continue;
            }

            if (!enOctaviano)
            {
                casosBorde.Add(new CasoBorde(
                    $"ClienteExternoId {id}: no está en octaviano.db (sí en Postgres, " +
                    $"sí en la planilla: {enPlanilla}).", id));
                discrepancias.Add(new Discrepancia(
                    id, saldoPostgres, null, enPlanilla ? saldoPlanilla : null,
                    CausaDiscrepancia.FaltaEnOctaviano));
                continue;
            }

            if (!enPlanilla)
            {
                casosBorde.Add(new CasoBorde(
                    $"ClienteExternoId {id}: no está en la planilla (sí en Postgres y en octaviano.db).",
                    id));
                discrepancias.Add(new Discrepancia(
                    id, saldoPostgres, saldoOctaviano, null, CausaDiscrepancia.FaltaEnPlanilla));
                continue;
            }

            // I4: compare through the single rounding point, exactly like every other monetary
            // comparison in the system — never a second, ad hoc rounding rule invented here.
            var rPostgres = Redondeo.Monto(saldoPostgres);
            var rOctaviano = Redondeo.Monto(saldoOctaviano);
            var rPlanilla = Redondeo.Monto(saldoPlanilla);

            if (rPostgres != rOctaviano || rPostgres != rPlanilla || rOctaviano != rPlanilla)
            {
                discrepancias.Add(new Discrepancia(
                    id, rPostgres, rOctaviano, rPlanilla, CausaDiscrepancia.MontoDistinto));
            }
        }

        return new ResultadoComparacion(
            TotalSociosPostgres: saldosPostgres.Count,
            TotalSociosOctaviano: saldosOctaviano.Count,
            TotalFilasPlanillaValidas: saldosPlanilla.Count,
            TotalSociosComparados: todosLosIds.Count,
            Discrepancias: discrepancias,
            CasosBorde: casosBorde,
            SumaPostgres: saldosPostgres.Values.Sum(),
            SumaOctaviano: saldosOctaviano.Values.Sum(),
            SumaPlanilla: saldosPlanilla.Values.Sum());
    }
}
