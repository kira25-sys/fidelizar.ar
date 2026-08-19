using Fidelizar.Domain.Entities;
using Fidelizar.Domain.Repositories;

namespace Fidelizar.Application.Services;

/// <summary>See <see cref="ICierreDiarioService"/>.</summary>
public sealed class CierreDiarioService(
    IMovimientoRepository movimientoRepository,
    IUsuarioRepository usuarioRepository,
    IMiembroRepository miembroRepository) : ICierreDiarioService
{
    public async Task<CierreDiarioResultado> ObtenerAsync(
        int negocioId, int sucursalId, DateOnly fecha, CancellationToken cancellationToken = default)
    {
        var canjesDelDia = await movimientoRepository.GetPorFechaEfectivaYTipoAsync(
            negocioId, fecha, TipoMovimientoCredito.Canje, cancellationToken);

        var usuarios = await usuarioRepository.ListarAsync(negocioId, cancellationToken);
        var usuariosPorId = usuarios.ToDictionary(u => u.Id);

        // A Canje's own branch is the cashier's (see the interface doc) — a movement with no
        // UsuarioId at all (sistema) never belongs to a branch's cierre.
        var canjesDeLaSucursal = canjesDelDia
            .Where(m => m.UsuarioId is { } usuarioId
                && usuariosPorId.TryGetValue(usuarioId, out var cajero)
                && cajero.SucursalId == sucursalId)
            .ToList();

        var items = new List<CierreDiarioItem>(canjesDeLaSucursal.Count);
        foreach (var movimiento in canjesDeLaSucursal)
        {
            var miembro = await miembroRepository.GetByIdAsync(negocioId, movimiento.MiembroId, cancellationToken);
            var cajero = usuariosPorId[movimiento.UsuarioId!.Value];

            // Canje rows store a negative Monto (SaldoService.RegistrarCanjeAsync) — the report
            // shows what was actually redeemed, a positive figure, not the ledger's own sign.
            items.Add(new CierreDiarioItem(
                miembro?.Nombre ?? $"Miembro {movimiento.MiembroId}",
                -movimiento.Monto,
                cajero.NombreCompleto,
                movimiento.RegistradoEn,
                movimiento.Motivo));
        }

        var total = items.Sum(i => i.Monto);

        return new CierreDiarioResultado(sucursalId, fecha, total, items);
    }
}
