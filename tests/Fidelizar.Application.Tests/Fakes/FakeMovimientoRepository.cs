using System.Reflection;
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
    // MovimientoCredito.Id is private-set, exactly like a real database-generated key — set via
    // reflection here the same way EF Core's own materialiser would, so GetByIdAsync has
    // something distinct to look up (Fidelizar.Infrastructure.Tests' equivalent fake does the
    // same for Miembro.Id).
    private static readonly PropertyInfo IdProperty = typeof(MovimientoCredito).GetProperty(nameof(MovimientoCredito.Id))!;

    private readonly List<MovimientoCredito> _movimientos = [];
    private long _nextId = 1;

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
        IdProperty.SetValue(movimiento, _nextId++);
        _movimientos.Add(movimiento);
        return Task.FromResult(movimiento);
    }
}
