using Fidelizar.Domain.Entities;
using Fidelizar.Domain.Repositories;
using Fidelizar.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Fidelizar.Infrastructure.Repositories;

/// <summary>EF Core implementation of <see cref="IUsuarioRepository"/>.</summary>
public sealed class UsuarioRepository(FidelizarDbContext dbContext) : IUsuarioRepository
{
    public Task<Usuario?> ObtenerPorEmailAsync(
        int negocioId, string email, CancellationToken cancellationToken = default) =>
        dbContext.Usuarios.SingleOrDefaultAsync(
            u => u.NegocioId == negocioId && u.Email == email, cancellationToken);

    public async Task<IReadOnlyList<Usuario>> ListarAsync(int negocioId, CancellationToken cancellationToken = default) =>
        await dbContext.Usuarios
            .Where(u => u.NegocioId == negocioId)
            .OrderBy(u => u.NombreCompleto)
            .ToListAsync(cancellationToken);

    public async Task<Usuario> CrearAsync(Usuario usuario, CancellationToken cancellationToken = default)
    {
        dbContext.Usuarios.Add(usuario);
        await dbContext.SaveChangesAsync(cancellationToken);
        return usuario;
    }

    public Task DesactivarAsync(Usuario usuario, CancellationToken cancellationToken = default) =>
        // usuario is already tracked by this same scoped DbContext (it came from
        // ObtenerPorEmailAsync or CrearAsync earlier in the request) and Usuario.Desactivar()
        // only flips Activo — the row is never removed (DATA-MODEL §2), so there is nothing here
        // beyond persisting the change the change tracker already sees.
        dbContext.SaveChangesAsync(cancellationToken);
}
