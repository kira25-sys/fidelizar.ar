namespace Fidelizar.Api.Security;

/// <summary>
/// Role identifiers as <c>[Authorize(Roles = ...)]</c> will use them once F1-03 wires up real
/// authentication (ARCHITECTURE §8). Defined now so a role name is a compile-time constant, never
/// a typed-by-hand string in a controller attribute. Values match DATA-MODEL §2 exactly:
/// <c>Dueno</c>, no "ñ" — "Dueño" is UI text only, never an identifier.
///
/// No <c>[Authorize]</c> attribute uses these yet and no policy is registered — that is F1-03's
/// job. This class only exists so both sides agree on the name in advance.
/// </summary>
public static class Roles
{
    public const string Cajero = "Cajero";
    public const string Encargada = "Encargada";
    public const string Dueno = "Dueno";
    public const string Soporte = "Soporte";
}
