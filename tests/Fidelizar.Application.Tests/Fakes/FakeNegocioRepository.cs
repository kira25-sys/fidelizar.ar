using Fidelizar.Domain.Entities;
using Fidelizar.Domain.Exceptions;
using Fidelizar.Domain.Repositories;

namespace Fidelizar.Application.Tests.Fakes;

/// <summary>In-memory stand-in for <see cref="INegocioRepository"/> (ARCHITECTURE §11). Mirrors
/// the real repository's fail-loud behaviour so <c>AuthServiceTests</c> can exercise it too.</summary>
public sealed class FakeNegocioRepository : INegocioRepository
{
    private readonly List<Negocio> _negociosActivos;

    public FakeNegocioRepository(params Negocio[] negociosActivos)
    {
        _negociosActivos = [.. negociosActivos];
    }

    public Task<Negocio> ObtenerUnicoAsync(CancellationToken cancellationToken = default)
    {
        if (_negociosActivos.Count != 1)
        {
            throw new ConflictException(
                $"Se esperaba exactamente un Negocio activo y se encontraron {_negociosActivos.Count}.",
                "NEGOCIO_UNICO_ESPERADO");
        }

        return Task.FromResult(_negociosActivos[0]);
    }
}
