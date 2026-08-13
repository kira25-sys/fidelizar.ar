using System.Globalization;
using Microsoft.Data.Sqlite;

namespace Fidelizar.VerificacionGate.Octaviano;

public sealed record SocioOctaviano(string ClienteExternoId, decimal Saldo);

/// <summary>
/// Punta 2 (ROADMAP F0-11): the balance Octaviano's own <c>VipSaldoService</c> returns today —
/// obtained <b>without referencing or executing a single line of Octaviano's source</b> (CLAUDE.md:
/// Octaviano is frozen, read-only, and this product carries no shared package with it — "copy the
/// code, do not reference it").
///
/// <para>
/// <b>Why reading raw rows and summing them here is faithful to what Octaviano returns today.</b>
/// <c>VipSaldoService.GetFichaAsync</c> (<c>Octaviano.Core/Services/VipSaldoService.cs</c>,
/// read-only reference code) computes the balance with exactly one line:
/// <c>await _movimientoRepository.GetSaldoAsync(miembro.Id, cancellationToken)</c>. That method's
/// entire body (<c>Octaviano.Infrastructure/Data/VipMovimientoRepository.cs</c>) is:
/// </para>
/// <code>
/// var montos = await _dbContext.VipMovimientos
///     .Where(m =&gt; m.MiembroId == miembroId)
///     .Select(m =&gt; m.Monto)
///     .ToListAsync(cancellationToken);
/// return montos.Sum();
/// </code>
/// <para>
/// No filter beyond the member id, no date window, no business rule — every row for that member,
/// summed as <c>decimal</c> <b>in memory</b> (Octaviano's own comment on that file explains why:
/// EF stores <c>decimal</c> as SQLite TEXT, so a SQL-side <c>SUM</c> would depend on SQLite's own
/// text-to-number coercion instead of exact decimal arithmetic — the same reason this product's I4
/// forbids <c>double</c> for money). This class issues the equivalent read — the same two tables,
/// no filter, opened read-only (<see cref="SqliteOpenMode.ReadOnly"/>, same as
/// <c>SqliteOctavianoSource</c> in <c>Fidelizar.MigracionOctaviano</c>) — and reproduces the exact
/// same grouping and the exact same <c>decimal</c> summation. The number produced is Octaviano's
/// number, not a reinterpretation of it, and nothing here writes to <c>octaviano.db</c> or changes
/// what Octaviano itself would compute if asked right now.
/// </para>
/// </summary>
public static class OctavianoLedgerSource
{
    public static IReadOnlyList<SocioOctaviano> Leer(string rutaBaseDatos)
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = rutaBaseDatos,
            Mode = SqliteOpenMode.ReadOnly,
        };

        using var connection = new SqliteConnection(builder.ConnectionString);
        connection.Open();

        var clienteExternoIdPorMiembro = new Dictionary<int, string>();
        using (var comandoMiembros = connection.CreateCommand())
        {
            comandoMiembros.CommandText = "SELECT Id, ClienteExternoId FROM VipMiembros;";
            using var lector = comandoMiembros.ExecuteReader();
            while (lector.Read())
            {
                var id = lector.GetInt32(0);
                var clienteExternoId = lector.GetInt32(1).ToString(CultureInfo.InvariantCulture);
                clienteExternoIdPorMiembro[id] = clienteExternoId;
            }
        }

        var saldoPorMiembro = new Dictionary<int, decimal>();
        using (var comandoMovimientos = connection.CreateCommand())
        {
            // Identical query to VipMovimientoRepository.GetSaldoAsync, just for every member at
            // once instead of one call per member: no WHERE beyond selecting the two columns.
            comandoMovimientos.CommandText = "SELECT MiembroId, Monto FROM VipMovimientos;";
            using var lector = comandoMovimientos.ExecuteReader();
            while (lector.Read())
            {
                var miembroId = lector.GetInt32(0);
                var monto = LeerDecimal(lector, 1);
                saldoPorMiembro[miembroId] = saldoPorMiembro.GetValueOrDefault(miembroId, 0m) + monto;
            }
        }

        var resultado = new List<SocioOctaviano>(clienteExternoIdPorMiembro.Count);
        foreach (var (miembroId, clienteExternoId) in clienteExternoIdPorMiembro)
        {
            var saldo = saldoPorMiembro.GetValueOrDefault(miembroId, 0m);
            resultado.Add(new SocioOctaviano(clienteExternoId, saldo));
        }

        return resultado;
    }

    /// <summary>Same reading rule as <c>SqliteOctavianoSource.LeerDecimal</c> in
    /// <c>Fidelizar.MigracionOctaviano</c> (F0-09) — kept identical on purpose.</summary>
    private static decimal LeerDecimal(SqliteDataReader lector, int ordinal)
    {
        var valor = lector.GetValue(ordinal);
        return valor switch
        {
            string texto => decimal.Parse(texto, NumberStyles.Number | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture),
            _ => Convert.ToDecimal(valor, CultureInfo.InvariantCulture),
        };
    }
}
