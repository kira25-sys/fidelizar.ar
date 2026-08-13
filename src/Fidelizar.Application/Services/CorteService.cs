using Fidelizar.Domain.Entities;
using Fidelizar.Domain.Exceptions;
using Fidelizar.Domain.Repositories;

namespace Fidelizar.Application.Services;

/// <summary>See <see cref="ICorteService"/>.</summary>
public sealed class CorteService(ICorteRepository corteRepository) : ICorteService
{
    public async Task<Corte> ObtenerCorteVigenteAsync(int negocioId, CancellationToken cancellationToken = default)
    {
        var corte = await corteRepository.ObtenerAsync(negocioId, cancellationToken);

        if (corte is null)
        {
            // F0-07: no default, no "today" guess — a hard-coded or invented cutoff double
            // credits every purchase between the real cutoff and the import day.
            throw new ConflictException(
                $"El negocio {negocioId} todavía no tiene un corte declarado. La acumulación de " +
                "crédito no puede correr sin él — hay que declarar uno primero al importar el " +
                "padrón (F0-08/F0-09).",
                "CORTE_NO_DECLARADO");
        }

        return corte;
    }

    public async Task<Corte> DeclararCorteAsync(
        int negocioId, DateOnly fecha, int declaradoPorUsuarioId, CancellationToken cancellationToken = default)
    {
        var corte = Corte.Declarar(negocioId, fecha, declaradoPorUsuarioId, DateTime.UtcNow);
        return await corteRepository.DeclararAsync(corte, cancellationToken);
    }
}
