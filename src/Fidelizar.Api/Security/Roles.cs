namespace Fidelizar.Api.Security;

/// <summary>
/// Role identifiers, used both by <c>[Authorize(Roles = ...)]</c> and by the policies in
/// <see cref="Policies"/> registered in <c>AuthenticationConfigurationExtensions</c>. A compile
/// time constant, never a typed-by-hand string in a controller attribute. Values match
/// DATA-MODEL §2 exactly: <c>Dueno</c>, no "ñ" — "Dueño" is UI text only, never an identifier.
///
/// Deliberately excludes <see cref="Fidelizar.Domain.Entities.RolUsuario.Sistema"/>: it is never
/// granted a policy and never presented in a token (<c>AuthService</c> refuses to authenticate
/// it). See <c>RolUsuarioRolesSincronizadosTests</c> for the test that keeps this list and the
/// Domain enum from drifting apart.
/// </summary>
public static class Roles
{
    public const string Cajero = "Cajero";
    public const string Encargada = "Encargada";
    public const string Dueno = "Dueno";
    public const string Soporte = "Soporte";
}
