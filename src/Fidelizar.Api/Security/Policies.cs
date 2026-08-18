namespace Fidelizar.Api.Security;

/// <summary>
/// Authorization policy names for <c>[Authorize(Policy = ...)]</c>, registered with
/// <c>AddAuthorizationBuilder</c> in <c>AuthenticationConfigurationExtensions</c> (ARCHITECTURE
/// §8; see <c>RolePolicyMappingTests</c> for the registration proof). Named after the role
/// hierarchy in ARCHITECTURE §8 ("Encargada can do everything a cashier can, plus…") — Soporte is
/// deliberately not part of this ladder, it is a separate audited lane.
/// </summary>
public static class Policies
{
    public const string CajeroOrAbove = "CajeroOrAbove";
    public const string EncargadaOrAbove = "EncargadaOrAbove";
    public const string DuenoOnly = "DuenoOnly";
}
