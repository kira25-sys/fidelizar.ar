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

    private readonly List<Miembro> _miembros = [];
    private int _nextId = 1;

    public IReadOnlyList<Miembro> Miembros => _miembros;

    public Task<Miembro?> GetByClienteExternoIdAsync(
        int negocioId, string clienteExternoId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_miembros.FirstOrDefault(
            m => m.NegocioId == negocioId && m.ClienteExternoId == clienteExternoId));

    public Task<Miembro> AddAsync(Miembro miembro, CancellationToken cancellationToken = default)
    {
        IdProperty.SetValue(miembro, _nextId++);
        _miembros.Add(miembro);
        return Task.FromResult(miembro);
    }
}
