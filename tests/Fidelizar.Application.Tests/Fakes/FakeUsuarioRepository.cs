using System.Reflection;
using Fidelizar.Domain.Entities;
using Fidelizar.Domain.Repositories;

namespace Fidelizar.Application.Tests.Fakes;

/// <summary>In-memory stand-in for <see cref="IUsuarioRepository"/> (ARCHITECTURE §11).</summary>
public sealed class FakeUsuarioRepository : IUsuarioRepository
{
    // Usuario.Id is private-set, like a real database-generated key — assigned here via
    // reflection the same way EF Core's own materialiser would, so tests that join a movement's
    // UsuarioId back to a Usuario's name (S7/S9) have something distinct to match on.
    private static readonly PropertyInfo IdProperty = typeof(Usuario).GetProperty(nameof(Usuario.Id))!;

    private readonly List<Usuario> _usuarios = [];
    private int _nextId = 1;

    public FakeUsuarioRepository(params Usuario[] usuarios)
    {
        foreach (var usuario in usuarios)
        {
            IdProperty.SetValue(usuario, _nextId++);
            _usuarios.Add(usuario);
        }
    }

    public Task<Usuario?> ObtenerPorEmailAsync(
        int negocioId, string email, CancellationToken cancellationToken = default) =>
        Task.FromResult(_usuarios.FirstOrDefault(u =>
            u.NegocioId == negocioId && string.Equals(u.Email, email, StringComparison.OrdinalIgnoreCase)));

    public Task<IReadOnlyList<Usuario>> ListarAsync(int negocioId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Usuario>>(_usuarios.Where(u => u.NegocioId == negocioId).ToList());

    public Task<Usuario> CrearAsync(Usuario usuario, CancellationToken cancellationToken = default)
    {
        IdProperty.SetValue(usuario, _nextId++);
        _usuarios.Add(usuario);
        return Task.FromResult(usuario);
    }

    public Task DesactivarAsync(Usuario usuario, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
