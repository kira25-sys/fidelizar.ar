namespace Fidelizar.Api.Security;

/// <summary>
/// Authorization policy names for <c>[Authorize(Policy = ...)]</c>. No policy is registered with
/// <c>AddAuthorizationBuilder</c> yet — that is F1-03's job, once real authentication exists
/// (ARCHITECTURE §8). Named after the role hierarchy in ARCHITECTURE §8 ("Encargada can do
/// everything a cashier can, plus…") so the eventual registration and every controller that
/// consumes it agree on the name in advance.
/// </summary>
public static class Policies
{
    public const string CajeroOrAbove = "CajeroOrAbove";
    public const string EncargadaOrAbove = "EncargadaOrAbove";
    public const string DuenoOnly = "DuenoOnly";
}
