using Fidelizar.Domain.Entities;
using Fidelizar.Domain.Repositories;

namespace Fidelizar.Infrastructure.Tests.Import.Fakes;

/// <summary>
/// In-memory stand-in for <see cref="IMovimientoRepository"/>, local to the importer tests
/// (ARCHITECTURE §11). Mirrors <c>Fidelizar.Application.Tests.Fakes.FakeMovimientoRepository</c>
/// — duplicated rather than shared, since each test project stays close to its own layer and
/// there is no shared test-support package between them (same reasoning as "no shared package
/// with Octaviano", Plan §1, applied one level down).
/// </summary>
public sealed class FakeMovimientoRepository : IMovimientoRepository
{
    private readonly List<MovimientoCredito> _movimientos = [];

    public IReadOnlyList<MovimientoCredito> Movimientos => _movimientos;

    public Task<decimal> GetSaldoAsync(int negocioId, int miembroId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_movimientos
            .Where(m => m.NegocioId == negocioId && m.MiembroId == miembroId)
            .Sum(m => m.Monto));

    public Task<IReadOnlyList<MovimientoCredito>> GetPorMiembroAsync(
        int negocioId, int miembroId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<MovimientoCredito>>(_movimientos
            .Where(m => m.NegocioId == negocioId && m.MiembroId == miembroId)
            .OrderByDescending(m => m.RegistradoEn)
            .ToList());

    public Task<IReadOnlyList<MovimientoCredito>> GetPorPeriodoAsync(
        int negocioId, string periodo, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<MovimientoCredito>>(_movimientos
            .Where(m => m.NegocioId == negocioId && m.Periodo == periodo)
            .ToList());

    public Task<bool> TieneMovimientosAsync(int negocioId, int miembroId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_movimientos.Any(m => m.NegocioId == negocioId && m.MiembroId == miembroId));

    public Task<MovimientoCredito?> GetByIdAsync(int negocioId, long id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_movimientos.FirstOrDefault(m => m.NegocioId == negocioId && m.Id == id));

    public Task<IReadOnlyList<MovimientoCredito>> GetPorFechaEfectivaYTipoAsync(
        int negocioId, DateOnly fechaEfectiva, TipoMovimientoCredito tipo, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<MovimientoCredito>>(_movimientos
            .Where(m => m.NegocioId == negocioId && m.FechaEfectiva == fechaEfectiva && m.Tipo == tipo)
            .OrderBy(m => m.RegistradoEn)
            .ToList());

    public Task<MovimientoCredito> AppendAsync(MovimientoCredito movimiento, CancellationToken cancellationToken = default)
    {
        _movimientos.Add(movimiento);
        return Task.FromResult(movimiento);
    }
}
