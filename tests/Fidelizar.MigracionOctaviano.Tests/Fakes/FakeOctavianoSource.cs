using Fidelizar.MigracionOctaviano.Origen;

namespace Fidelizar.MigracionOctaviano.Tests.Fakes;

/// <summary>
/// In-memory stand-in for <see cref="IOctavianoSource"/> — never opens a SQLite file. Every test
/// in this project builds its own <see cref="OctavianoMiembro"/>/<see cref="OctavianoMovimiento"/>
/// rows with invented names and ids (CLAUDE.md: a real member is never a test case).
/// </summary>
public sealed class FakeOctavianoSource(
    IReadOnlyList<OctavianoMiembro> miembros,
    IReadOnlyList<OctavianoMovimiento> movimientos,
    OctavianoCorte? corte = null)
    : IOctavianoSource
{
    public Task<IReadOnlyList<TablaEsquema>> LeerEsquemaAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<TablaEsquema>>([]);

    public Task<IReadOnlyList<OctavianoMiembro>> LeerMiembrosAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(miembros);

    public Task<IReadOnlyList<OctavianoMovimiento>> LeerMovimientosAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(movimientos);

    public Task<OctavianoCorte?> LeerCorteAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(corte);
}
