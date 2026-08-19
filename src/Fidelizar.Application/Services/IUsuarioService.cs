using Fidelizar.Domain.Entities;

namespace Fidelizar.Application.Services;

public sealed record UsuarioResultado(
    int Id, string NombreCompleto, string Email, RolUsuario Rol, int? SucursalId, bool Activo);

/// <param name="NegocioId">Required, not a convention (I8).</param>
/// <param name="Password">Plain text, in memory only for the length of this call — hashed before
/// anything reaches a repository (DATA-MODEL §2, ASP.NET Core Identity's hasher).</param>
public sealed record CrearUsuarioSolicitud(
    int NegocioId, string NombreCompleto, string Email, string Password, RolUsuario Rol, int? SucursalId);

/// <summary>
/// S10 Usuarios (Dueño only, FUNCTIONAL-SPEC §screen-map).
/// <see cref="Fidelizar.Domain.Repositories.IUsuarioRepository"/> only had what
/// <c>AuthService</c> needed for login — this is the CRUD S10 actually asks for, in this phase:
/// list and create. There is no delete: <c>Usuario</c> only ever deactivates (DATA-MODEL §2), and
/// nothing in this task's scope wires that action to an endpoint yet.
/// </summary>
public interface IUsuarioService
{
    Task<IReadOnlyList<UsuarioResultado>> ListarAsync(int negocioId, CancellationToken cancellationToken = default);

    /// <exception cref="Fidelizar.Domain.Exceptions.ConflictException">
    /// A user with that email already exists for this business.
    /// </exception>
    /// <exception cref="Fidelizar.Domain.Exceptions.ValidationException">
    /// <c>SucursalId</c> does not exist for this business, or the role/branch combination is
    /// invalid (DATA-MODEL §2 — <c>Usuario.Crear</c> enforces this half).
    /// </exception>
    Task<UsuarioResultado> CrearAsync(CrearUsuarioSolicitud solicitud, CancellationToken cancellationToken = default);
}
