using Fidelizar.Domain.Entities;
using Fidelizar.Domain.Exceptions;
using Fidelizar.Domain.Repositories;

namespace Fidelizar.Application.Services;

/// <summary>See <see cref="IAnulacionMovimientoService"/>.</summary>
public sealed class AnulacionMovimientoService(IMovimientoRepository movimientoRepository) : IAnulacionMovimientoService
{
    public async Task<MovimientoCredito> AnularAsync(
        AnularMovimientoRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.ClaveIdempotencia))
        {
            throw new ValidationException(
                "La clave de idempotencia es obligatoria (README decisión #6).",
                "CLAVE_IDEMPOTENCIA_REQUERIDA");
        }

        var original = await movimientoRepository.GetByIdAsync(request.NegocioId, request.MovimientoId, cancellationToken)
            ?? throw new EntityNotFoundException($"Movimiento {request.MovimientoId}");

        // README decision #6, extended to S8 2026-08-21: a retry with the same key returns the
        // Ajuste already written instead of moving the money a second time. Checked before
        // anything is built, exactly like RegistrarCanjeAsync does.
        var existente = await movimientoRepository.GetPorClaveIdempotenciaAsync(
            request.NegocioId, request.ClaveIdempotencia, cancellationToken);
        if (existente is not null)
        {
            return CoincideConElReintento(existente, original, request)
                ? existente
                : throw ClaveReutilizadaException();
        }

        // I1/I3: never an edit, never a delete — the correction is a new Ajuste of the exact
        // opposite amount, dated today (when the void happens), carrying the mandatory reason
        // and the acting user. MovimientoCredito.Crear enforces Motivo for every Ajuste on its
        // own; passing it here is not an extra guard, it is the same one, once.
        var ajuste = MovimientoCredito.Crear(
            negocioId: request.NegocioId,
            miembroId: original.MiembroId,
            fechaEfectiva: request.Hoy,
            registradoEn: DateTime.UtcNow,
            tipo: TipoMovimientoCredito.Ajuste,
            monto: -original.Monto,
            hoy: request.Hoy,
            usuarioId: request.UsuarioId,
            motivo: request.Motivo,
            claveIdempotencia: request.ClaveIdempotencia);

        try
        {
            return await movimientoRepository.AppendAsync(ajuste, cancellationToken);
        }
        catch (ConflictException)
        {
            // Lost the race: a concurrent request with the same key committed first. The unique
            // partial index on (NegocioId, ClaveIdempotencia) is what actually stopped this one —
            // the check above cannot. The winner is the real result either way.
            var ganador = await movimientoRepository.GetPorClaveIdempotenciaAsync(
                request.NegocioId, request.ClaveIdempotencia, cancellationToken);

            return ganador is not null && CoincideConElReintento(ganador, original, request)
                ? ganador
                : throw ClaveReutilizadaException();
        }
    }

    /// <summary>
    /// Whether the movement already on record under this key is the same void — a true retry.
    /// The ledger stores no pointer from an <c>Ajuste</c> to the row it corrects (DATA-MODEL §4),
    /// so the comparison is the void's own shape: member, exact opposite amount, reason.
    /// <c>FechaEfectiva</c> is deliberately excluded — it is the server's own "today", and a
    /// retry that crosses midnight is still the same attempt.
    /// </summary>
    private static bool CoincideConElReintento(
        MovimientoCredito existente, MovimientoCredito original, AnularMovimientoRequest request) =>
        existente.Tipo == TipoMovimientoCredito.Ajuste
        && existente.MiembroId == original.MiembroId
        && existente.Monto == -original.Monto
        && existente.Motivo == request.Motivo;

    private static ConflictException ClaveReutilizadaException() => new(
        "Esta clave de idempotencia ya se usó para una anulación con otros datos. " +
        "No es un reintento válido.",
        "CLAVE_IDEMPOTENCIA_REUTILIZADA");
}
