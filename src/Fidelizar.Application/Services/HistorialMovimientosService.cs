using Fidelizar.Domain.Exceptions;
using Fidelizar.Domain.Repositories;

namespace Fidelizar.Application.Services;

/// <summary>See <see cref="IHistorialMovimientosService"/>.</summary>
public sealed class HistorialMovimientosService(
    IMiembroRepository miembroRepository,
    IMovimientoRepository movimientoRepository,
    IUsuarioRepository usuarioRepository) : IHistorialMovimientosService
{
    public async Task<IReadOnlyList<MovimientoHistorialItem>> ObtenerAsync(
        int negocioId, int miembroId, CancellationToken cancellationToken = default)
    {
        _ = await miembroRepository.GetByIdAsync(negocioId, miembroId, cancellationToken)
            ?? throw new EntityNotFoundException($"Miembro {miembroId}");

        var movimientos = await movimientoRepository.GetPorMiembroAsync(negocioId, miembroId, cancellationToken);

        // One bulk fetch instead of one lookup per row — a business's staff list is small
        // (phase-1 scale), so this stays a single round trip regardless of history length.
        var usuarios = await usuarioRepository.ListarAsync(negocioId, cancellationToken);
        var nombresPorUsuarioId = usuarios.ToDictionary(u => u.Id, u => u.NombreCompleto);

        return movimientos
            .Select(m => new MovimientoHistorialItem(
                m.Id,
                m.Tipo,
                m.Monto,
                m.FechaEfectiva,
                m.RegistradoEn,
                m.Motivo,
                m.SaldoResultante,
                m.UsuarioId is { } usuarioId && nombresPorUsuarioId.TryGetValue(usuarioId, out var nombre) ? nombre : null))
            .ToList();
    }
}
