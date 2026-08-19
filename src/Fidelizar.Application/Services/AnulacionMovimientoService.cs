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
        var original = await movimientoRepository.GetByIdAsync(request.NegocioId, request.MovimientoId, cancellationToken)
            ?? throw new EntityNotFoundException($"Movimiento {request.MovimientoId}");

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
            motivo: request.Motivo);

        return await movimientoRepository.AppendAsync(ajuste, cancellationToken);
    }
}
