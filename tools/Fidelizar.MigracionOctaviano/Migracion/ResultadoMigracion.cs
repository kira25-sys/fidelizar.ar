namespace Fidelizar.MigracionOctaviano.Migracion;

/// <summary>
/// A skipped record, identified <b>only</b> by <c>ClienteExternoId</c> (CLAUDE.md, ROADMAP F0-09)
/// — never by name, phone or DNI. <see cref="Motivo"/> is a fixed, static reason string chosen by
/// this tool's own code (never built from a source field), so it can never leak personal data by
/// accident.
/// </summary>
public sealed record RegistroSalteado(string ClienteExternoId, string Motivo);

/// <summary>What one migration run did. Every field here is a count or a
/// <see cref="RegistroSalteado"/> — nothing that could hold a name, a phone or a DNI.</summary>
public sealed record ResultadoMigracion(
    int NegocioId,
    int ConfiguracionId,
    int MiembrosCreados,
    int MiembrosYaExistian,
    IReadOnlyList<RegistroSalteado> MiembrosSalteados,
    int MovimientosMigrados,
    int SociosConMovimientosYaMigrados,
    IReadOnlyList<RegistroSalteado> MovimientosSalteados,
    int ConsentimientosCreados,
    int ConsentimientosYaExistian);
