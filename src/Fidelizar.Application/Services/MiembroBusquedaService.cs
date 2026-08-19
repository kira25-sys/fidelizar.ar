using Fidelizar.Domain.Exceptions;
using Fidelizar.Domain.Repositories;
using Fidelizar.Domain.Texto;

namespace Fidelizar.Application.Services;

/// <summary>See <see cref="IMiembroBusquedaService"/>.</summary>
public sealed class MiembroBusquedaService(
    IMiembroRepository miembroRepository, IMovimientoRepository movimientoRepository) : IMiembroBusquedaService
{
    private const int LargoMinimoNormalizado = 3;

    public async Task<IReadOnlyList<MiembroBusquedaResultado>> BuscarAsync(
        int negocioId, string query, CancellationToken cancellationToken = default)
    {
        var normalizada = VipNombres.Normalizar(query);

        // FUNCTIONAL-SPEC §4: "buscar, nunca listar" — a query too short to be a real search
        // never falls through to an empty-string match that would return the whole roster.
        if (normalizada.Length < LargoMinimoNormalizado)
        {
            throw new ValidationException(
                "La búsqueda necesita al menos 3 letras.", "BUSQUEDA_MUY_CORTA");
        }

        var palabras = normalizada.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        var candidatos = await miembroRepository.BuscarAsync(negocioId, palabras, cancellationToken);

        var resultados = new List<MiembroBusquedaResultado>(candidatos.Count);
        foreach (var miembro in candidatos)
        {
            var saldo = await movimientoRepository.GetSaldoAsync(negocioId, miembro.Id, cancellationToken);
            resultados.Add(new MiembroBusquedaResultado(miembro.Id, miembro.Nombre, miembro.NumeroSocio, saldo, miembro.FechaAlta));
        }

        return resultados;
    }
}
