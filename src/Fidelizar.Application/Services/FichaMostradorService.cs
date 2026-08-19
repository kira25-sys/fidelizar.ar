using Fidelizar.Domain.Exceptions;
using Fidelizar.Domain.Reglas;
using Fidelizar.Domain.Repositories;

namespace Fidelizar.Application.Services;

/// <summary>See <see cref="IFichaMostradorService"/>.</summary>
public sealed class FichaMostradorService(
    IMiembroRepository miembroRepository,
    IMovimientoRepository movimientoRepository,
    ICorteService corteService) : IFichaMostradorService
{
    public async Task<FichaMostradorResultado> ObtenerAsync(
        int negocioId, int miembroId, DateOnly hoy, CancellationToken cancellationToken = default)
    {
        var miembro = await miembroRepository.GetByIdAsync(negocioId, miembroId, cancellationToken)
            ?? throw new EntityNotFoundException($"Miembro {miembroId}");

        var saldo = await movimientoRepository.GetSaldoAsync(negocioId, miembroId, cancellationToken);
        var corte = await corteService.ObtenerCorteVigenteAsync(negocioId, cancellationToken);

        var alertas = new List<AlertaMiembroResultado>();

        // RN-11: only day and month matter, notice starts 2 days before.
        if (AvisoCumpleanos.DebeAvisar(miembro.FechaNacimiento, hoy))
        {
            var fecha = miembro.FechaNacimiento!.Value;
            alertas.Add(new AlertaMiembroResultado(
                TipoAlertaMiembro.Cumpleanos, $"Cumple el {fecha.Day}/{fecha.Month}"));
        }

        return new FichaMostradorResultado(
            miembro.Id, miembro.Nombre, miembro.NumeroSocio, saldo, corte.Fecha, alertas);
    }
}
