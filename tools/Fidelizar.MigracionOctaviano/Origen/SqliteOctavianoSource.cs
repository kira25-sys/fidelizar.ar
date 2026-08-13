using System.Globalization;
using Microsoft.Data.Sqlite;

namespace Fidelizar.MigracionOctaviano.Origen;

/// <summary>
/// Reads Octaviano's real <c>octaviano.db</c>. <b>Read-only, always</b> — the connection string
/// opens the file with SQLite's own <c>Mode=ReadOnly</c> flag (not just "we promise not to
/// write"), so even a bug here cannot turn into a write against the source database
/// (CLAUDE.md, ARCHITECTURE §4 I9's same spirit applied to the migration's own source).
///
/// Schema learned from Octaviano's own EF Core migrations
/// (<c>Octaviano.Infrastructure/Data/Migrations/20260726152518_AddVipTables.cs</c> and
/// <c>20260810193511_AddVipCorte.cs</c>, read-only reference code per CLAUDE.md): <c>VipMiembros</c>,
/// <c>VipMovimientos</c>, <c>VipCortes</c>, <c>VipVentasMensuales</c>. This migration only reads
/// the first two — the member roster and the full ledger (ROADMAP F0-09); <c>VipVentasMensuales</c>
/// is a rebuildable cache (Octaviano's own docs say so) and <c>VipCortes</c> is out of scope here
/// (see the F0-09 report's "decisions" section).
/// </summary>
public sealed class SqliteOctavianoSource(string rutaBaseDatos) : IOctavianoSource
{
    private SqliteConnection AbrirConexionSoloLectura()
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = rutaBaseDatos,
            Mode = SqliteOpenMode.ReadOnly,
        };

        var connection = new SqliteConnection(builder.ConnectionString);
        connection.Open();
        return connection;
    }

    public async Task<IReadOnlyList<TablaEsquema>> LeerEsquemaAsync(CancellationToken cancellationToken = default)
    {
        using var connection = AbrirConexionSoloLectura();

        var nombresTablas = new List<string>();
        await using (var comandoTablas = connection.CreateCommand())
        {
            comandoTablas.CommandText =
                "SELECT name FROM sqlite_master WHERE type = 'table' AND name LIKE 'Vip%' ORDER BY name;";
            await using var lector = await comandoTablas.ExecuteReaderAsync(cancellationToken);
            while (await lector.ReadAsync(cancellationToken))
            {
                nombresTablas.Add(lector.GetString(0));
            }
        }

        var tablas = new List<TablaEsquema>();
        foreach (var nombreTabla in nombresTablas)
        {
            var columnas = new List<ColumnaEsquema>();
            await using var comandoColumnas = connection.CreateCommand();
            // PRAGMA table_info never returns row content — only the table's own column
            // metadata (name, declared type, nullability, PK), which is exactly what CLAUDE.md
            // allows to appear in the report.
            comandoColumnas.CommandText = $"PRAGMA table_info('{nombreTabla}');";
            await using var lectorColumnas = await comandoColumnas.ExecuteReaderAsync(cancellationToken);
            while (await lectorColumnas.ReadAsync(cancellationToken))
            {
                var nombreColumna = lectorColumnas.GetString(1);
                var tipoColumna = lectorColumnas.IsDBNull(2) ? "" : lectorColumnas.GetString(2);
                columnas.Add(new ColumnaEsquema(nombreColumna, tipoColumna));
            }

            tablas.Add(new TablaEsquema(nombreTabla, columnas));
        }

        return tablas;
    }

    public async Task<IReadOnlyList<OctavianoMiembro>> LeerMiembrosAsync(CancellationToken cancellationToken = default)
    {
        using var connection = AbrirConexionSoloLectura();
        await using var comando = connection.CreateCommand();
        comando.CommandText =
            """
            SELECT Id, ClienteExternoId, NumeroVip, Nombre, Telefono, Dni, FechaNacimiento,
                   SucursalExternaId, FechaAlta, Activo, UpdatedAt
            FROM VipMiembros;
            """;

        var resultado = new List<OctavianoMiembro>();
        await using var lector = await comando.ExecuteReaderAsync(cancellationToken);
        while (await lector.ReadAsync(cancellationToken))
        {
            resultado.Add(new OctavianoMiembro(
                Id: lector.GetInt32(0),
                ClienteExternoId: lector.GetInt32(1),
                NumeroVip: LeerTextoNullable(lector, 2),
                Nombre: lector.GetString(3),
                Telefono: LeerTextoNullable(lector, 4),
                Dni: LeerTextoNullable(lector, 5),
                FechaNacimiento: LeerFechaNullable(lector, 6),
                SucursalExternaId: lector.IsDBNull(7) ? null : lector.GetInt32(7),
                FechaAlta: LeerFecha(lector, 8),
                Activo: LeerBool(lector, 9),
                UpdatedAt: LeerFechaHora(lector, 10)));
        }

        return resultado;
    }

    public async Task<IReadOnlyList<OctavianoMovimiento>> LeerMovimientosAsync(CancellationToken cancellationToken = default)
    {
        using var connection = AbrirConexionSoloLectura();
        await using var comando = connection.CreateCommand();
        comando.CommandText =
            """
            SELECT Id, MiembroId, Fecha, Periodo, Tipo, Monto, ReferenciaVenta, Usuario, Motivo,
                   SaldoResultante
            FROM VipMovimientos;
            """;

        var resultado = new List<OctavianoMovimiento>();
        await using var lector = await comando.ExecuteReaderAsync(cancellationToken);
        while (await lector.ReadAsync(cancellationToken))
        {
            resultado.Add(new OctavianoMovimiento(
                Id: lector.GetInt32(0),
                MiembroId: lector.GetInt32(1),
                Fecha: LeerFechaHora(lector, 2),
                Periodo: lector.GetString(3),
                Tipo: lector.GetInt32(4),
                Monto: LeerDecimal(lector, 5),
                ReferenciaVenta: lector.IsDBNull(6) ? null : LeerInt64(lector, 6),
                Usuario: lector.GetString(7),
                Motivo: LeerTextoNullable(lector, 8),
                SaldoResultante: LeerDecimal(lector, 9)));
        }

        return resultado;
    }

    private static string? LeerTextoNullable(SqliteDataReader lector, int ordinal) =>
        lector.IsDBNull(ordinal) ? null : lector.GetString(ordinal);

    private static bool LeerBool(SqliteDataReader lector, int ordinal) =>
        Convert.ToBoolean(lector.GetValue(ordinal), CultureInfo.InvariantCulture);

    private static long LeerInt64(SqliteDataReader lector, int ordinal) =>
        Convert.ToInt64(lector.GetValue(ordinal), CultureInfo.InvariantCulture);

    /// <summary>
    /// EF Core's SQLite provider stores <c>decimal</c> as TEXT, invariant-formatted, to keep
    /// full precision (SQLite's only numeric storage classes are INTEGER and REAL/double, and a
    /// double cannot represent every decimal exactly — the same reason I4 forbids <c>double</c>
    /// for money in this product). Read as the raw value and converted through
    /// <see cref="Convert.ToDecimal(object, IFormatProvider)"/> rather than a fixed-format
    /// parse, so a REAL or INTEGER storage class (should one ever appear) still converts
    /// correctly instead of throwing.
    /// </summary>
    private static decimal LeerDecimal(SqliteDataReader lector, int ordinal)
    {
        var valor = lector.GetValue(ordinal);
        return valor switch
        {
            string texto => decimal.Parse(texto, NumberStyles.Number | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture),
            _ => Convert.ToDecimal(valor, CultureInfo.InvariantCulture),
        };
    }

    private static DateOnly LeerFecha(SqliteDataReader lector, int ordinal) =>
        DateOnly.FromDateTime(LeerFechaHora(lector, ordinal));

    private static DateOnly? LeerFechaNullable(SqliteDataReader lector, int ordinal) =>
        lector.IsDBNull(ordinal) ? null : LeerFecha(lector, ordinal);

    /// <summary>
    /// EF Core's SQLite provider stores <c>DateOnly</c>/<c>DateTime</c> as ISO-8601 TEXT.
    /// <see cref="DateTime.Parse(string, IFormatProvider, DateTimeStyles)"/> reads that format
    /// (with or without a time component, with or without fractional seconds) without needing an
    /// exact format string.
    /// </summary>
    private static DateTime LeerFechaHora(SqliteDataReader lector, int ordinal)
    {
        var valor = lector.GetValue(ordinal);
        return valor switch
        {
            DateTime fecha => fecha,
            string texto => DateTime.Parse(texto, CultureInfo.InvariantCulture, DateTimeStyles.None),
            _ => throw new FormatException($"No se pudo interpretar como fecha el valor de tipo {valor.GetType()}."),
        };
    }
}
