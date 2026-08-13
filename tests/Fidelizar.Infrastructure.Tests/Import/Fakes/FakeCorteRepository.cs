using Fidelizar.Domain.Entities;
using Fidelizar.Domain.Repositories;

namespace Fidelizar.Infrastructure.Tests.Import.Fakes;

/// <summary>In-memory stand-in for <see cref="ICorteRepository"/>, local to the importer tests
/// (ARCHITECTURE §11).</summary>
public sealed class FakeCorteRepository : ICorteRepository
{
    private readonly Dictionary<int, Corte> _cortes = [];

    public Task<Corte?> ObtenerAsync(int negocioId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_cortes.GetValueOrDefault(negocioId));

    public Task<Corte> DeclararAsync(Corte corte, CancellationToken cancellationToken = default)
    {
        _cortes[corte.NegocioId] = corte;
        return Task.FromResult(corte);
    }
}
