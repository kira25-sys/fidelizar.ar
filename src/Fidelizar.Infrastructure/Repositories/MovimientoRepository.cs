using Fidelizar.Domain.Entities;
using Fidelizar.Domain.Exceptions;
using Fidelizar.Domain.Repositories;
using Fidelizar.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Fidelizar.Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IMovimientoRepository"/>. Exposes no Update and no
/// Delete (I1) — there is no method here that could ever issue one against
/// <c>MovimientosCredito</c>.
/// </summary>
public sealed class MovimientoRepository(FidelizarDbContext dbContext) : IMovimientoRepository
{
    public Task<decimal> GetSaldoAsync(int negocioId, int miembroId, CancellationToken cancellationToken = default) =>
        dbContext.MovimientosCredito
            .Where(m => m.NegocioId == negocioId && m.MiembroId == miembroId)
            .SumAsync(m => m.Monto, cancellationToken);

    public async Task<IReadOnlyList<MovimientoCredito>> GetPorMiembroAsync(
        int negocioId, int miembroId, CancellationToken cancellationToken = default) =>
        await dbContext.MovimientosCredito
            .Where(m => m.NegocioId == negocioId && m.MiembroId == miembroId)
            .OrderByDescending(m => m.RegistradoEn)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<MovimientoCredito>> GetPorPeriodoAsync(
        int negocioId, string periodo, CancellationToken cancellationToken = default) =>
        await dbContext.MovimientosCredito
            .Where(m => m.NegocioId == negocioId && m.Periodo == periodo)
            .ToListAsync(cancellationToken);

    public Task<bool> TieneMovimientosAsync(int negocioId, int miembroId, CancellationToken cancellationToken = default) =>
        dbContext.MovimientosCredito
            .AnyAsync(m => m.NegocioId == negocioId && m.MiembroId == miembroId, cancellationToken);

    public Task<MovimientoCredito?> GetByIdAsync(int negocioId, long id, CancellationToken cancellationToken = default) =>
        dbContext.MovimientosCredito.SingleOrDefaultAsync(m => m.NegocioId == negocioId && m.Id == id, cancellationToken);

    public Task<MovimientoCredito?> GetPorClaveIdempotenciaAsync(
        int negocioId, string claveIdempotencia, CancellationToken cancellationToken = default) =>
        dbContext.MovimientosCredito.SingleOrDefaultAsync(
            m => m.NegocioId == negocioId && m.ClaveIdempotencia == claveIdempotencia, cancellationToken);

    public async Task<IReadOnlyList<MovimientoCredito>> GetPorFechaEfectivaYTipoAsync(
        int negocioId, DateOnly fechaEfectiva, TipoMovimientoCredito tipo, CancellationToken cancellationToken = default) =>
        await dbContext.MovimientosCredito
            .Where(m => m.NegocioId == negocioId && m.FechaEfectiva == fechaEfectiva && m.Tipo == tipo)
            .OrderBy(m => m.RegistradoEn)
            .ToListAsync(cancellationToken);

    public async Task<MovimientoCredito> AppendAsync(
        MovimientoCredito movimiento, CancellationToken cancellationToken = default)
    {
        // I2: SaldoResultante is historical evidence, computed from the current SUM(Monto)
        // inside the same transaction as the insert, so a concurrent append can never read a
        // stale balance. Serializable isolation closes the race a plain read-then-insert would
        // leave open between two simultaneous redemptions for the same member.
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable, cancellationToken);

        var saldoActual = await dbContext.MovimientosCredito
            .Where(m => m.NegocioId == movimiento.NegocioId && m.MiembroId == movimiento.MiembroId)
            .SumAsync(m => m.Monto, cancellationToken);

        movimiento.FijarSaldoResultante(saldoActual + movimiento.Monto);

        dbContext.MovimientosCredito.Add(movimiento);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (EsViolacionDeClaveIdempotencia(ex))
        {
            // README decision #6: two concurrent retries with the same key both reached this
            // point past the application-level check in SaldoService/AnulacionMovimientoService —
            // the unique partial index on (NegocioId, ClaveIdempotencia) is what actually stops
            // the second row, and this is that stop surfacing. The caller re-reads the winner via
            // GetPorClaveIdempotenciaAsync and returns it instead of treating this as a failure.
            await transaction.RollbackAsync(cancellationToken);
            throw new ConflictException(
                "Ya existe un movimiento registrado con esta clave de idempotencia.",
                "CLAVE_IDEMPOTENCIA_EN_USO");
        }

        return movimiento;
    }

    private static bool EsViolacionDeClaveIdempotencia(DbUpdateException ex) =>
        ex.InnerException is PostgresException pg
        && pg.SqlState == PostgresErrorCodes.UniqueViolation
        && pg.ConstraintName == "IX_MovimientosCredito_NegocioId_ClaveIdempotencia";
}
