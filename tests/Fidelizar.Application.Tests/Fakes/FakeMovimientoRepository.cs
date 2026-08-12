using Fidelizar.Domain.Entities;
using Fidelizar.Domain.Repositories;

namespace Fidelizar.Application.Tests.Fakes;

/// <summary>
/// In-memory stand-in for <see cref="IMovimientoRepository"/> so <c>Fidelizar.Application.Tests</c>
/// runs with no database (ARCHITECTURE §11). Mirrors the real repository's shape exactly — no
/// Update, no Delete (I1) — and computes the balance the same way the real one must:
/// <c>SUM(Monto)</c>, never a stored column (I2).
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

    public Task<MovimientoCredito> AppendAsync(MovimientoCredito movimiento, CancellationToken cancellationToken = default)
    {
        _movimientos.Add(movimiento);
        return Task.FromResult(movimiento);
    }
}
