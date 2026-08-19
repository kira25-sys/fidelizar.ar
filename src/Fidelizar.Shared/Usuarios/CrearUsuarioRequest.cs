using System.ComponentModel.DataAnnotations;

namespace Fidelizar.Shared.Usuarios;

/// <summary>S10 Usuarios — create (Dueño only). <c>Rol</c> travels as text, parsed against
/// <c>Fidelizar.Domain.Entities.RolUsuario</c> server-side (Shared cannot reference Domain,
/// ARCHITECTURE §3) — <c>Sistema</c> is rejected there even though the CLR enum has a fourth
/// value, because no account may ever be created under it.</summary>
public sealed record CrearUsuarioRequest(
    [Required(ErrorMessage = "El nombre es obligatorio.")]
    string NombreCompleto,
    [Required(ErrorMessage = "El email es obligatorio.")]
    [EmailAddress(ErrorMessage = "El email no es válido.")]
    string Email,
    [Required(ErrorMessage = "La contraseña es obligatoria.")]
    string Password,
    [Required(ErrorMessage = "El rol es obligatorio.")]
    string Rol,
    int? SucursalId);
