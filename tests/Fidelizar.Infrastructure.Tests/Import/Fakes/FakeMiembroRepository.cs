using System.Reflection;
using Fidelizar.Domain.Entities;
using Fidelizar.Domain.Repositories;

namespace Fidelizar.Infrastructure.Tests.Import.Fakes;

/// <summary>
/// In-memory stand-in for <see cref="IMiembroRepository"/>, local to the importer tests
/// (ARCHITECTURE §11 — fast, no database). Assigns an auto-incrementing <c>Id</c> the same way a
/// real database would; <c>Miembro.Id</c> is <c>init</c>-only, so this uses reflection to set it
/// after construction the same way EF Core's own materialiser does — a real
/// <see cref="Fidelizar.Infrastructure.Repositories.MiembroRepository"/> needs no such trick,
/// because EF Core sets init-only properties natively when it reads a generated key back.
/// </summary>
public sealed class FakeMiembroRepository : IMiembroRepository
{
    private static readonly PropertyInfo IdProperty =
        typeof(Miembro).GetProperty(nameof(Miembro.Id))!;

    private static readonly PropertyInfo ClienteExternoIdProperty =
        typeof(Miembro).GetProperty(nameof(Miembro.ClienteExternoId))!;

    private static readonly PropertyInfo ActualizadoEnProperty =
        typeof(Miembro).GetProperty(nameof(Miembro.ActualizadoEn))!;

    private readonly List<Miembro> _miembros = [];
    private int _nextId = 1;

    public IReadOnlyList<Miembro> Miembros => _miembros;

    public Task<Miembro?> GetByClienteExternoIdAsync(
        int negocioId, string clienteExternoId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_miembros.FirstOrDefault(
            m => m.NegocioId == negocioId && m.ClienteExternoId == clienteExternoId));

    public Task<Miembro?> GetByIdAsync(int negocioId, int id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_miembros.FirstOrDefault(m => m.NegocioId == negocioId && m.Id == id));

    public Task<IReadOnlyList<Miembro>> BuscarAsync(
        int negocioId, IReadOnlyList<string> palabrasNormalizadas, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Miembro>>(_miembros
            .Where(m => m.NegocioId == negocioId
                && palabrasNormalizadas.All(p => m.NombreNormalizado.Contains(p, StringComparison.Ordinal)))
            .ToList());

    public Task<Miembro> AddAsync(Miembro miembro, CancellationToken cancellationToken = default)
    {
        IdProperty.SetValue(miembro, _nextId++);
        _miembros.Add(miembro);
        return Task.FromResult(miembro);
    }

    // Not exercised by the importer — a minimal, correct implementation only, so this fake keeps
    // satisfying IMiembroRepository as it grows (F1-14).
    public Task<IReadOnlyList<Miembro>> ListarSinVincularAsync(
        int negocioId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Miembro>>(_miembros
            .Where(m => m.NegocioId == negocioId && m.ClienteExternoId is null)
            .OrderBy(m => m.FechaAlta)
            .ToList());

    public Task<bool> VincularClienteExternoAsync(
        int negocioId,
        int miembroId,
        string clienteExternoId,
        DateTime ahoraUtc,
        CancellationToken cancellationToken = default)
    {
        var miembro = _miembros.FirstOrDefault(
            m => m.NegocioId == negocioId && m.Id == miembroId && m.ClienteExternoId is null);

        if (miembro is null)
        {
            return Task.FromResult(false);
        }

        ClienteExternoIdProperty.SetValue(miembro, clienteExternoId);
        ActualizadoEnProperty.SetValue(miembro, ahoraUtc);
        return Task.FromResult(true);
    }
}
