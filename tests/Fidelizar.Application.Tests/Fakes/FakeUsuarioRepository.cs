using Fidelizar.Domain.Entities;
using Fidelizar.Domain.Repositories;

namespace Fidelizar.Application.Tests.Fakes;

/// <summary>In-memory stand-in for <see cref="IUsuarioRepository"/> (ARCHITECTURE §11).</summary>
public sealed class FakeUsuarioRepository : IUsuarioRepository
{
    private readonly List<Usuario> _usuarios = [];

    public FakeUsuarioRepository(params Usuario[] usuarios)
    {
        _usuarios.AddRange(usuarios);
    }

    public Task<Usuario?> ObtenerPorEmailAsync(
        int negocioId, string email, CancellationToken cancellationToken = default) =>
        Task.FromResult(_usuarios.FirstOrDefault(u =>
            u.NegocioId == negocioId && string.Equals(u.Email, email, StringComparison.OrdinalIgnoreCase)));

    public Task<Usuario> CrearAsync(Usuario usuario, CancellationToken cancellationToken = default)
    {
        _usuarios.Add(usuario);
        return Task.FromResult(usuario);
    }

    public Task DesactivarAsync(Usuario usuario, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
