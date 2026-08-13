namespace Fidelizar.MigracionOctaviano.Origen;

/// <summary>
/// Read-only access to Octaviano's SQLite database. One implementation
/// (<see cref="SqliteOctavianoSource"/>) reads the real file; tests supply a fake built from
/// invented data (CLAUDE.md: a real member is never a test case) — this interface is the seam
/// that lets <see cref="Migracion.MigradorOctaviano"/> be tested with no SQLite file at all.
/// </summary>
public interface IOctavianoSource
{
    /// <summary>Column names and declared types only — never row content (CLAUDE.md).</summary>
    Task<IReadOnlyList<TablaEsquema>> LeerEsquemaAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OctavianoMiembro>> LeerMiembrosAsync(CancellationToken cancellationToken = default);

    /// <summary>Every ledger row, for every member, in one call — the migrator groups and orders
    /// them itself. Append-only history, not a single collapsed balance (ROADMAP F0-09).</summary>
    Task<IReadOnlyList<OctavianoMovimiento>> LeerMovimientosAsync(CancellationToken cancellationToken = default);
}
