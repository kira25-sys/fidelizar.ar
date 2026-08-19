namespace Fidelizar.Shared.Usuarios;

/// <summary>S10 Usuarios (Dueño only). Never carries <c>PasswordHash</c> — this is the same
/// discipline that keeps <c>Auth.SesionResponse</c> free of it.</summary>
public sealed record UsuarioResponse(
    int Id, string NombreCompleto, string Email, string Rol, int? SucursalId, bool Activo);
