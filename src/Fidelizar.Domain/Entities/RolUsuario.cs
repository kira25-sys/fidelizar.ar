namespace Fidelizar.Domain.Entities;

/// <summary>
/// A user's role (DATA-MODEL §2, ARCHITECTURE §8). Persisted as <c>int</c>: values are never
/// reordered and never reused. No "ñ" in the identifier — "Dueño" is UI text only. The four
/// person-facing names match <c>Fidelizar.Api.Security.Roles</c>, so <c>Rol.ToString()</c> is the
/// claim value <c>[Authorize(Roles = ...)]</c> expects.
/// </summary>
public enum RolUsuario
{
    Cajero = 0,
    Encargada = 1,
    Dueno = 2,
    Soporte = 3,

    /// <summary>
    /// Not a person: the identity a non-interactive process stamps on a movement, a cutoff or a
    /// program configuration when there is no staff member to credit. Never granted a policy and
    /// never presented in a token — <c>AuthService</c> refuses to authenticate it under any
    /// circumstance. It exists to be pointed at, never to sign in.
    /// </summary>
    Sistema = 4,
}
