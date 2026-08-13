namespace Fidelizar.Domain.Entities;

/// <summary>
/// A user's role (DATA-MODEL §2, ARCHITECTURE §8). Persisted as <c>int</c> — values are <b>never
/// reordered and never reused</b>, the same discipline as <see cref="TipoMovimientoCredito"/>. No
/// "ñ" in the identifier: "Dueño" is UI text only, never an identifier (DATA-MODEL §2). The names
/// match <c>Fidelizar.Api.Security.Roles</c> exactly, so <c>Rol.ToString()</c> is the claim value
/// <c>[Authorize(Roles = ...)]</c> expects, with no translation step in between.
/// </summary>
public enum RolUsuario
{
    Cajero = 0,
    Encargada = 1,
    Dueno = 2,
    Soporte = 3,
}
