using Fidelizar.Domain.Entities;
using Fidelizar.Domain.Repositories;
using Fidelizar.Domain.Texto;

namespace Fidelizar.Application.Tests.Fakes;

/// <summary>
/// In-memory stand-in for <see cref="IMiembroRepository"/> so <c>Fidelizar.Application.Tests</c>
/// runs with no database (ARCHITECTURE §11). Test fixtures only — every member here is invented
/// (CLAUDE.md).
/// </summary>
public sealed class FakeMiembroRepository : IMiembroRepository
{
    private readonly List<Miembro> _miembros = [];

    public void Sembrar(Miembro miembro) => _miembros.Add(miembro);

    /// <summary>Builds and seeds an invented member with just enough to exercise a lookup.</summary>
    public Miembro SembrarNuevo(int negocioId, int id, string nombre = "Socia De Prueba")
    {
        var miembro = new Miembro
        {
            Id = id,
            NegocioId = negocioId,
            Nombre = nombre,
            NombreNormalizado = VipNombres.Normalizar(nombre),
            FechaAlta = new DateOnly(2026, 1, 1),
        };

        Sembrar(miembro);
        return miembro;
    }

    public Task<Miembro?> GetByClienteExternoIdAsync(
        int negocioId, string clienteExternoId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_miembros.SingleOrDefault(
            m => m.NegocioId == negocioId && m.ClienteExternoId == clienteExternoId));

    public Task<Miembro?> GetByIdAsync(int negocioId, int id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_miembros.SingleOrDefault(m => m.NegocioId == negocioId && m.Id == id));

    public Task<IReadOnlyList<Miembro>> BuscarAsync(
        int negocioId, IReadOnlyList<string> palabrasNormalizadas, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Miembro>>(_miembros
            .Where(m => m.NegocioId == negocioId
                && palabrasNormalizadas.All(p => m.NombreNormalizado.Contains(p, StringComparison.Ordinal)))
            .OrderBy(m => m.Nombre)
            .ToList());

    public Task<Miembro> AddAsync(Miembro miembro, CancellationToken cancellationToken = default)
    {
        _miembros.Add(miembro);
        return Task.FromResult(miembro);
    }
}
