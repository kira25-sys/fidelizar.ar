namespace Fidelizar.MigracionOctaviano.Migracion;

/// <summary>
/// A skipped record, identified <b>only</b> by <c>ClienteExternoId</c> (CLAUDE.md, ROADMAP F0-09)
/// — never by name, phone or DNI. <see cref="Motivo"/> is a fixed, static reason string chosen by
/// this tool's own code (never built from a source field), so it can never leak personal data by
/// accident.
/// </summary>
public sealed record RegistroSalteado(string ClienteExternoId, string Motivo);

/// <summary>What one migration run did. Every field here is a count, a date, or a
/// <see cref="RegistroSalteado"/> — nothing that could hold a name, a phone or a DNI.</summary>
/// <param name="CorteFecha">The cutoff date now on record for the business — Octaviano's own,
/// migrated unchanged — or null when Octaviano had none declared either.</param>
/// <param name="CorteAdvertencia">Set only when Octaviano's cutoff disagreed with one already on
/// record in Fidelizar: <c>ICorteRepository</c> has no update, so the existing one is left
/// untouched and this explains why (mirrors <c>VipPadronImporter</c>'s own cutoff-mismatch
/// handling).</param>
public sealed record ResultadoMigracion(
    int NegocioId,
    int ConfiguracionId,
    DateOnly? CorteFecha,
    string? CorteAdvertencia,
    int MiembrosCreados,
    int MiembrosYaExistian,
    IReadOnlyList<RegistroSalteado> MiembrosSalteados,
    int MovimientosMigrados,
    int SociosConMovimientosYaMigrados,
    IReadOnlyList<RegistroSalteado> MovimientosSalteados,
    int ConsentimientosCreados,
    int ConsentimientosYaExistian);
