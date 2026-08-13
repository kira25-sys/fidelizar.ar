namespace Fidelizar.MigracionOctaviano.Origen;

/// <summary>
/// One row of Octaviano's <c>VipMiembros</c> table, read as-is. <c>ClienteExternoId</c> is
/// <c>int</c> here (Octaviano's shape) — the migrator converts it to <c>text</c> when writing
/// <see cref="Fidelizar.Domain.Entities.Miembro"/> (DATA-MODEL §3, §7).
/// </summary>
public sealed record OctavianoMiembro(
    int Id,
    int ClienteExternoId,
    string? NumeroVip,
    string Nombre,
    string? Telefono,
    string? Dni,
    DateOnly? FechaNacimiento,
    int? SucursalExternaId,
    DateOnly FechaAlta,
    bool Activo,
    DateTime UpdatedAt);

/// <summary>One row of Octaviano's <c>VipMovimientos</c> table, read as-is.</summary>
public sealed record OctavianoMovimiento(
    int Id,
    int MiembroId,
    DateTime Fecha,
    string Periodo,
    int Tipo,
    decimal Monto,
    long? ReferenciaVenta,
    string Usuario,
    string? Motivo,
    decimal SaldoResultante);

/// <summary>
/// Column name and declared SQLite type of one column, for the schema-only report (CLAUDE.md:
/// "si querés mostrarme la forma de una tabla, mostrame el esquema, nunca su contenido").
/// </summary>
public sealed record ColumnaEsquema(string Nombre, string Tipo);

public sealed record TablaEsquema(string Nombre, IReadOnlyList<ColumnaEsquema> Columnas);
