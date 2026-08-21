namespace Fidelizar.SeederDesarrollo.Sembrado;

/// <summary>
/// What one run did. Every count is split into "created" and "already existed" because that split
/// is the evidence that this tool is idempotent — a second run reports zeros on the left and the
/// same totals on the right.
/// </summary>
public sealed record ResultadoSembrado(
    int NegocioId,
    bool NegocioCreado,
    DateOnly Corte,
    bool CorteDeclarado,
    int SucursalesCreadas,
    int SucursalesYaExistian,
    int UsuariosCreados,
    int UsuariosYaExistian,
    int MiembrosCreados,
    int MiembrosYaExistian,
    int ConsentimientosCreados,
    int ConsentimientosYaExistian,
    int MovimientosCreados,
    int MiembrosConHistorialPrevio,
    IReadOnlyList<SaldoSembrado> Saldos);

/// <summary>A seeded member's resulting balance, always recomputed as <c>SUM(Monto)</c> after the
/// writes, never carried over from the fixture (I2).</summary>
public sealed record SaldoSembrado(string ClienteExternoId, string Nombre, decimal Saldo);
