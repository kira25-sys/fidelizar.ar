using Fidelizar.Domain.Entities;
using Fidelizar.Domain.Exceptions;
using Fidelizar.Domain.Repositories;

namespace Fidelizar.Application.Services;

/// <summary>
/// Balance queries and redemptions. The balance is always <c>SUM(Monto)</c> — computed by
/// <see cref="IMovimientoRepository.GetSaldoAsync"/>, never read from a stored column (I2).
/// Knows nothing about HTTP or EF (ARCHITECTURE §3).
/// </summary>
public sealed class SaldoService(IMovimientoRepository movimientoRepository) : ISaldoService
{
    public Task<decimal> ObtenerSaldoAsync(int negocioId, int miembroId, CancellationToken cancellationToken = default) =>
        movimientoRepository.GetSaldoAsync(negocioId, miembroId, cancellationToken);

    public async Task<MovimientoCredito> RegistrarCanjeAsync(
        RegistrarCanjeRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Monto <= 0m)
        {
            throw new ValidationException(
                "El monto del canje tiene que ser mayor a cero.", "MONTO_INVALIDO");
        }

        var saldo = await movimientoRepository.GetSaldoAsync(request.NegocioId, request.MiembroId, cancellationToken);

        // RN-25: the only thing that can put a balance below zero is a system-generated Ajuste.
        // While it is negative, every human redemption is blocked outright — not merely capped.
        if (saldo < 0m)
        {
            throw new ValidationException(
                "El saldo del socio está en revisión (negativo, RN-25). No se pueden registrar " +
                "canjes hasta que se resuelva.",
                "SALDO_EN_REVISION");
        }

        // I6 / RN-24: a redemption never exceeds the available balance. A larger amount is a
        // typo and is rejected — no human action may ever produce a negative balance.
        if (request.Monto > saldo)
        {
            throw new ValidationException(
                $"El canje (${request.Monto:N2}) supera el saldo disponible (${saldo:N2}).",
                "CANJE_SUPERA_SALDO");
        }

        var movimiento = MovimientoCredito.Crear(
            negocioId: request.NegocioId,
            miembroId: request.MiembroId,
            fechaEfectiva: request.FechaEfectiva,
            registradoEn: DateTime.UtcNow,
            tipo: TipoMovimientoCredito.Canje,
            monto: -request.Monto,
            hoy: request.Hoy,
            usuarioId: request.UsuarioId,
            motivo: request.Motivo);

        return await movimientoRepository.AppendAsync(movimiento, cancellationToken);
    }
}
